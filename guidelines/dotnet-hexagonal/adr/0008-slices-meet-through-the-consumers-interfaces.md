# ADR-0008: Slices meet through the consumer's interfaces, and never in a cycle

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

A command in one slice sometimes needs another slice's knowledge: submitting an answer needs
the exercise's grading, which belongs to the lessons slice. Letting it call the other slice's
operations made commands depend on queries ([ADR-0012](0012-commands-and-queries-stay-apart.md)
forbids that) and drew the boundary between slices at a folder name.

## Decision

- **A slice references only itself and `Common`, with one exception: it may implement an
  interface another slice declares in its `Domain`.** The consumer states its need in its own
  words (`Attempts/Domain/IGradeAnswer`); the slice that knows the answer implements it
  (`Lessons/…/AnswerGrader : IGradeAnswer`), through its own domain. This is dependency
  inversion between slices: the provider depends on the consumer's interface, the consumer on
  nothing of the provider's.
- **The dependency graph between slices is acyclic.** C# accepts namespace cycles inside one
  assembly, so the compiler will not object; the check does. Without it, two slices that
  implement each other's interfaces can no longer be read, changed or split into projects one
  at a time.
- Both are checked on compiler-resolved source
  ([ADR-0011](0011-slice-rules-checked-on-source.md)), each against a broken fixture.

## Alternatives considered

- **Call the other slice's operations**: simple, and common in modular monoliths, but a
  command then depends on the other slice's queries, and the boundary is a folder.
- **Adapters in `App.Host` implementing the consumer's interface**: keeps the provider ignorant
  of the consumer, at the cost of glue code outside the core and a test per adapter. Worth it
  when one slice starts implementing many others' interfaces.
- **Domain events and a local copy in the consumer**: decoupled in time, and far more
  machinery than one database and one transaction need.

## Consequences

- A slice's dependencies are visible as the interfaces it implements.
- The provider knows the consumer exists, against "depend in the direction of stability". The
  acyclic check keeps this from tangling; many such implementations in one slice are the
  signal to move the glue to `App.Host`.
- Splitting slices into projects stays possible: provider projects reference consumer
  projects, never the reverse.
