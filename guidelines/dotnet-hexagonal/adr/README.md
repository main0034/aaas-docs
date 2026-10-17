---
status: active
owner: martin
last-updated: 2026-10-17
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
| [0004](0004-operations-are-an-interface-a-contract-and-a-handler.md)   | Operations are an interface, a contract and a handler, kept together | Accepted |
| [0005](0005-adapters-as-separate-projects.md)                     | Adapters live in separate projects                    | Accepted |
| [0006](0006-formatting-with-csharpier.md)                         | Formatting with CSharpier                             | Accepted |
| [0007](0007-tunit-as-the-test-framework.md)                       | TUnit as the test framework                           | Accepted |
| [0008](0008-slices-meet-through-the-consumers-interfaces.md)      | Slices meet through the consumer's interfaces, never in a cycle | Accepted |
| [0009](0009-typed-primitives-on-generic-bases.md)                 | Typed primitives on generic bases                     | Accepted |
| [0010](0010-db-entities-keys-audit-columns-and-version.md)        | Db entities: naming, keys, audit columns, version     | Accepted |
| [0011](0011-slice-rules-checked-on-source.md)                     | Slice rules are checked on compiler-resolved source   | Accepted |
| [0012](0012-commands-and-queries-stay-apart.md)                   | Commands and queries stay apart                       | Accepted |

## Postponed

Decisions we have consciously deferred, so nobody quietly makes them in a pull request:

|                                         Question                                          |                Why postponed                 |            Decide by             |
|-------------------------------------------------------------------------------------------|----------------------------------------------|----------------------------------|
| How handlers are registered (explicit in `App.Host` vs an `AddCore()` extension in Core)  | Not enough experience with the trade-off yet | After the first few slices exist |
