# ADR-0003: Split aggregate creation into `Create` and `Hydrate`

- **Status:** Accepted
- **Date:** 2026-09-28 (ported from the Java set, 2026-08-21)
- **Deciders:** Martin

## Context

An aggregate comes into existence in two entirely different situations, which a single
constructor cannot distinguish:

1. **Something happens in the business.** A user opens an account, sets a goal, or
   creates a group. This is the moment a rule applies ("a new account may not be
   archived"), an identity is assigned, and, once we add them, a domain event is raised.
2. **We read stored state back.** The persistence adapter rebuilds an aggregate that
   already exists. No business event is happening; it happened months ago. Re-running
   creation logic here would raise a duplicate event, reset derived state, or reject an
   object that is legitimately in a state today's creation rules would not allow.

With one constructor these paths are indistinguishable. The difference tends to be
papered over with nullable parameters, boolean flags, or "if the id is `Guid.Empty` it's
new".

## Decision

Aggregates have **no public constructors**. They are created through exactly two named
paths:

- `static X Create(...)`: a genuine new instance. It applies creation-time business rules,
  assigns identity, sets initial state, and is the single place a future creation event
  would be recorded.
- `static X Hydrate(XState state)`: reconstitution from stored state. It accepts the
  aggregate exactly as stored and checks structural validity only (no nulls, consistent
  fields). It records no events, applies no defaults and sets no timestamps.

`Hydrate` is called **only by persistence mappers**. An ArchUnitNET test enforces this: no
type outside `App.Adapters.Persistence` may call a method named `Hydrate`. Tests build
aggregates with `Create`, or with a test builder that uses it. Only persistence-mapping
tests use `Hydrate`.

## Alternatives considered

- **A single constructor or factory for both paths**: fewer concepts. Rejected: it is
  exactly the ambiguity that makes domain events, defaults and identity assignment unsafe
  to add later.
- **`internal` `Hydrate` with `[InternalsVisibleTo("App.Adapters.Persistence")]`**: the
  compiler would enforce the restriction, which Java could not offer. Rejected:
  `InternalsVisibleTo` exposes *every* internal type in `App.Core` to the persistence
  adapter, not just `Hydrate`. That breaks "internal by default" for the whole assembly
  to protect one method. The architecture test is narrower.
- **A separate reconstitution interface per aggregate**: more explicit still, but more
  machinery than these applications need.

## Consequences

- The two paths must stay in sync as fields are added. Forgetting one is a compile error,
  since both call the one private constructor.
- `Hydrate` deliberately accepts states `Create` would reject: an archived account, a goal
  that ended last year. That asymmetry is intended, and should be stated in a comment
  where it is not obvious.
- Adding domain events later is a local change to `Create`, with no risk of firing them
  while reading from the database.
- Slightly more code per aggregate, and a rule contributors will not guess on their own.
  So it is in the repository's `AGENT.md` and in
  [guidelines/domain-modelling.md](../guidelines/domain-modelling.md), not only here.
