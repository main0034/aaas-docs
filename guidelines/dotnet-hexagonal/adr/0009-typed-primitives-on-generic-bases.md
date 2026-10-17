# ADR-0009: Typed primitives derive from generic bases closed over themselves

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

Every identifier and name was a hand-written record repeating the empty-check, `Value` and
`ToString`, and a postponed question asked whether to adopt a source generator. A plain
shared base would remove the repetition but let two id types be compared (they would share
a base type), losing the compile error the guideline promises.

## Decision

`TypedGuidBase<TSelf>` and `TypedStringBase<TSelf>` in `Common/Domain` hold `Value`, its
check and `ToString`. A type closes the base over itself: `UserId : TypedGuidBase<UserId>,
ITypedGuid<UserId>`. `ITypedGuid<TSelf>` declares `static abstract TSelf From(Guid)`, so the
base can offer `New()` (a UUIDv7) and the compiler demands `From`. Two types share no base but
`object`, so `userId == accountId` is CS0019. Architecture tests check that every domain type
with a public `Guid` or `string` `Value` derives from its base closed over itself, and compile
a cross-type comparison expecting CS0019. See
[domain-modelling](../guidelines/domain-modelling.md#typed-identifiers-and-names).

This settles the postponed "hand-written vs source generator" question.

## Alternatives considered

- **Unrelated records**: the same safety, the same few lines repeated in every type.
- **A non-generic base**: no repetition, but cross-type comparison compiles (always false).
- **A source generator** (Vogen, StronglyTypedId): a package and generated code to trust, for
  little at this size.

## Consequences

- One place for the check, `ToString` and `New()`; each type is a constructor and `From`.
- Closing the base over the wrong type compiles; the architecture test catches it.
- `New()` is a static member on a generic type: CA1000 is suppressed on that one method,
  since callers write `UserId.New()`.
