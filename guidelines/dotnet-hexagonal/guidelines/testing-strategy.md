---
status: draft
owner: martin
last-updated: 2026-09-28
---

# Testing strategy

The hexagon gives us a natural test pyramid: the interesting rules sit in the middle,
where tests are fastest.

## Levels

|         Level          |                     What it covers                      |                                                        How                                                         |                Speed                 |
|------------------------|---------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|--------------------------------------|
| **Domain tests**       | Aggregates, value objects, invariants, calculations     | xUnit v3 in `App.Core.Tests`, `new` and factories, no host                                                         | Milliseconds. The bulk of our tests  |
| **Handler tests**      | Orchestration and authorization                         | Handler constructed directly; outbound ports replaced by hand-written fakes; `FakeTimeProvider` for time           | Milliseconds                         |
| **Architecture tests** | Slice isolation and the conventions the compiler misses | ArchUnitNET, one test class in `App.Core.Tests`                                                                    | Milliseconds                         |
| **Adapter tests**      | Persistence mapping and queries; the HTTP contract      | Persistence: a real PostgreSQL (Testcontainers), migrations applied. Web: `WebApplicationFactory` with fake inbound ports | Seconds                              |
| **End-to-end tests**   | A few critical journeys                                 | `WebApplicationFactory` over the real host and a real PostgreSQL                                                   | Slow. Keep to a handful              |

Write the test at the **lowest level that can express the rule**. A rule about
withdrawals reducing goal progress is a domain test, not an end-to-end test.

`App.Core.Tests` references `App.Core` only. It has no ASP.NET Core and no EF Core on its
dependency graph, which is itself a useful guarantee: a domain test that needs either is
testing the wrong layer.

## Fakes over mocks

Outbound ports get hand-written in-memory fakes (`InMemoryAccounts : IAccounts`), kept in
the test project beside the tests that use them. They are reusable, they force the port to
stay narrow, and they fail honestly when the contract is wrong. A mock happily returns
whatever the test told it to. Mocking libraries are for the rare awkward case, not the
default.

**A fake is not a fake database.** The EF Core in-memory provider and SQLite do not
replace PostgreSQL anywhere. They hide exactly the differences that break in production:
translation, collation, constraints, and case-sensitivity. Persistence is tested against
PostgreSQL or not at all.

## What must be tested

The application's requirements name the rules with non-negotiable coverage, including
empty data and period boundaries. Reference them by number (`FR-3.8`) in the test class.
For the illustrative accounts domain, that would be:

- balance derivation, including an account with no transactions
- goal progress on the first and last day of a period
- visibility rules, including that a non-member sees nothing

## Architecture tests

The core/adapter dependency rule is enforced by the build itself: `App.Core` has no
framework on its dependency graph ([ADR-0005](../adr/0005-adapters-as-separate-projects.md)).
One ArchUnitNET test class covers what the compiler cannot see:

- `App.Core` references no `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*` or
  `Npgsql.*` type. The build makes this true today; the test keeps it true if someone adds
  a package to `App.Core`.
- `*.Domain` namespaces depend on no other project namespace except `Common.Domain`.
- No slice depends on another slice's `Domain`, `Commands` or `Queries`, only on its
  `Ports.In`.
- Every type in `*.Commands` or `*.Queries` ending in `Handler` implements an interface
  from its own slice's `Ports.In`.
- Only types in `App.Adapters.Persistence` call a method named `Hydrate`
  ([ADR-0003](../adr/0003-split-aggregate-creation-into-create-and-hydrate.md)).
- Aggregates declare no public constructors.

## Conventions

- Arrange / Act / Assert, separated by blank lines. Comments only where the setup is
  genuinely obscure.
- One assertion *concept* per test.
- **AwesomeAssertions only.** Every assertion goes through `.Should()`. xUnit's
  `Assert.Equal`, `Assert.True` and `Assert.Throws` are not used, so a test reads one way
  throughout, and failure messages describe the value, not just the mismatch.
  (AwesomeAssertions is the Apache-licensed community fork of FluentAssertions 7.
  FluentAssertions itself moved to a commercial licence from version 8.)

  ```csharp
  Username.From("  martin  ").Value.Should().Be("martin");
  UserId.From(a).Should().Be(UserId.From(a));
  ids.Should().HaveCount(2);
  result.Should().BeEquivalentTo([expectedFirst, expectedSecond], o => o.WithStrictOrdering());

  var act = () => Username.From("");
  act.Should().Throw<ArgumentException>().WithMessage("*value*");

  var deposit = async () => await handler.HandleAsync(command, ct);
  await deposit.Should().ThrowAsync<AccountArchived>();
  ```

  Prefer the assertion that states the intent (`BeEquivalentTo`, `ContainSingle`,
  `ThrowAsync<T>`) over unpacking the object and comparing fields yourself.

- Test data comes from small builders (`AnAccount().Archived().Build()`) that build
  through `Create`, so a test states only what matters to it. Never reach for `Hydrate` to
  fabricate a state the domain would refuse. If a test needs that, either the rule or the
  test is wrong. Persistence-mapping tests are the exception.
- Tests are deterministic: `FakeTimeProvider` (from `Microsoft.Extensions.TimeProvider.Testing`),
  fixed identifiers, no `Random`, no `Task.Delay`.
- Tests run in parallel by default. A test that shares a database uses its own schema or
  its own container, never an ordering assumption.
- A bug fix starts with a failing test that reproduces it.
- **TODO:** decide whether to enforce a coverage threshold in CI, and what to do about
  DTOs and migrations if we do.
