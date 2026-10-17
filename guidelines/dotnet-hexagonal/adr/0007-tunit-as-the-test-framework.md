# ADR-0007: TUnit as the test framework

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

The set used xUnit v3. Its tests run on Microsoft.Testing.Platform, which the .NET 10 SDK
requires for `dotnet test` with xUnit v3 anyway (VSTest is refused). TUnit is built on that
platform from the start, discovers tests with a source generator rather than reflection, runs
tests in parallel by default, and has first-class async fixtures.

## Decision

Tests use **TUnit**: `[Test]`, `[Arguments]` for cases, a `CancellationToken` parameter
instead of an ambient context, and `[ClassDataSource<T>(Shared = ...)]` for expensive fixtures
(`IAsyncInitializer` + `IAsyncDisposable`). Architecture tests use
`TngTech.ArchUnitNET.TUnit`.

**Assertions stay AwesomeAssertions.** Its `.Should()` throws like any assertion library, so
it works under TUnit unchanged, and the rule "one way to assert" holds. TUnit's `Assert.That`
is not used.

## Alternatives considered

- **xUnit v3**: works, and was the choice until now. Reflection discovery, fixture shapes
  (`IClassFixture`, `IAsyncLifetime`, `TestContext.Current`) that TUnit expresses more
  directly.
- **TUnit's own assertions**: async (`await Assert.That(x).IsEqualTo(y)`), so every assertion
  and its test change shape, and a second assertion style appears beside AwesomeAssertions in
  any code that keeps it. No gain worth that.
- **NUnit / MSTest**: no reason to prefer them over either.

## Consequences

- Test projects reference `TUnit` (which brings the engine and Microsoft.Testing.Platform)
  and no `Microsoft.NET.Test.Sdk`.
- Test conventions checked from source look for `[Test]`, not `[Fact]`/`[Theory]`.
- TUnit runs tests in parallel across and within classes: a test sharing state says so with
  `[NotInParallel]`, or, better, does not share it.
