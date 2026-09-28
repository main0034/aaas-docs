---
status: active
owner: martin
last-updated: 2026-09-28
---

# Architecture decision records

One file per decision that was not obvious, numbered in order. ADRs are **immutable**:
when a decision changes, write a new ADR and mark the old one `Superseded by ADR-nnnn`.

This lets `architecture/` describe only the present, while the reasoning stays available
for anyone, human or AI, who asks "why is it like this?".

These six were ported from a Java/Spring set of the same numbers (21–26 August 2026). The
decisions are the same. Where .NET changes the mechanism or the trade-off, the ADR says so
rather than pretending the original was written for C#.

Copy [0000-template.md](0000-template.md) to start one.

|                                 #                                 |                       Decision                        |  Status  |
|-------------------------------------------------------------------|-------------------------------------------------------|----------|
| [0001](0001-hexagonal-architecture-with-use-case-slices.md)       | Hexagonal architecture with use-case slices           | Accepted |
| [0002](0002-domain-free-of-ef-core.md)                            | The domain stays free of EF Core                      | Accepted |
| [0003](0003-split-aggregate-creation-into-create-and-hydrate.md)  | Split aggregate creation into `Create` and `Hydrate`  | Accepted |
| [0004](0004-command-and-query-handlers-behind-inbound-ports.md)   | Command and query handlers behind inbound ports       | Accepted |
| [0005](0005-adapters-as-separate-projects.md)                     | Adapters live in separate projects                    | Accepted |
| [0006](0006-formatting-with-csharpier.md)                         | Formatting with CSharpier                             | Accepted |

## Postponed

Decisions we have consciously deferred, so nobody quietly makes them in a pull request:

|                                         Question                                          |                Why postponed                 |            Decide by             |
|-------------------------------------------------------------------------------------------|----------------------------------------------|----------------------------------|
| How handlers are registered (explicit in `App.Host` vs an `AddCore()` extension in Core)  | Not enough experience with the trade-off yet | After the first few slices exist |
| Hand-written typed identifiers vs a source generator (Vogen, StronglyTypedId)              | Only a few identifiers exist                 | Past a dozen typed primitives    |
