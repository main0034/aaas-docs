# ADR-0002: The domain stays free of EF Core

- **Status:** Accepted
- **Date:** 2026-09-28 (ported from the Java set's "free of JPA annotations", 2026-08-21)
- **Deciders:** Martin

## Context

[ADR-0001](0001-hexagonal-architecture-with-use-case-slices.md) puts a framework-free
domain at the centre. The cheapest way to persist that domain is to let EF Core map the
aggregates themselves. Unlike JPA, EF Core can do this without a single attribute: fluent
`IEntityTypeConfiguration<T>` classes in the persistence project can map private fields
and a private constructor. So the question in .NET is subtler than in Java. It is not
"annotations in the domain or not", but "may EF Core materialize and track domain objects
at all".

Mapping the domain directly still makes the model answer to EF Core as well as to the
business, in ways the domain code does not show:

- EF materializes objects through a constructor or parameterless path of its choosing.
  That bypasses both `Create` and `Hydrate`
  ([ADR-0003](0003-split-aggregate-creation-into-create-and-hydrate.md)).
- The change tracker turns a property assignment inside an aggregate into an `UPDATE`,
  whether or not a handler meant to save it.
- Navigation properties invite object references across aggregate boundaries, and value
  objects become owned types or value converters with their own restrictions on
  constructors and equality.
- Lazy loading, if anyone enables it, lets a domain method issue a query.

None of this is visible in the domain code, which is what makes it expensive: the model
looks pure and behaves otherwise.

## Decision

`App.Core` does not reference EF Core, and EF Core never materializes a domain type.
`App.Adapters.Persistence` has its own EF entity classes (`AccountEntity`), plain
property bags holding `Guid`, `string`, `decimal` and the like. Hand-written mappers
translate between them and domain objects: `Hydrate` on the way in, reading state on the
way out. Outbound ports accept and return domain types. An EF entity never leaves the
persistence adapter.

The compiler enforces the first half ([ADR-0005](0005-adapters-as-separate-projects.md)).
An architecture test enforces that only persistence code calls `Hydrate`.

## Alternatives considered

- **Fluent-mapping the aggregates directly**: no twin classes and no mappers, and still
  no attributes in the domain. This is the strongest alternative in .NET, much stronger
  than JPA annotations were in Java. Rejected: it hands object creation and change
  detection to EF, which defeats the `Create`/`Hydrate` split and the explicit save.
  Revisit if the mappers become the dominant cost and the aggregates stay simple.
- **A mapping library (AutoMapper, Mapster)**: removes some boilerplate. Rejected: mapping
  is where the two models are reconciled, and doing it by hand keeps that visible and
  debuggable. AutoMapper also moved to a commercial licence in 2025.

## Consequences

- Every aggregate has a twin: a domain class and an EF entity, plus a mapper and its
  tests. This is the accepted cost, and the main argument the next contributor will make
  against this decision.
- The domain is unit-testable with `new` and plain xUnit, with no database and no host.
- The schema can change shape without the domain noticing, and vice versa. Typed
  identifiers need no EF value converters, because entities hold plain `Guid`s.
- Saving is explicit. A handler adds or updates through an outbound port and commits
  through `IUnitOfWork`. An update means the adapter loads the tracked entity and copies
  state onto it. That is more code than change tracking, and it is considered a benefit.
- Queries that return read models may project straight from EF entities to read-model
  records in the persistence adapter. That is the CQRS-lite escape hatch, not a leak.
