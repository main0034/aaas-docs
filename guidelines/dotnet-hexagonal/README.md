---
status: draft
owner: martin
last-updated: 2026-09-28
---

# .NET hexagonal guidelines

A reusable set of architecture guidelines for a C# / .NET 10 application built as a hexagon:
a framework-free domain, named commands and queries behind inbound ports, and adapters in
projects of their own. Ported on 28 September 2026 from a Java/Spring set written for
another project (August 2026). The principles are unchanged; the mechanisms are .NET's.
The worked examples use an illustrative accounts domain (accounts, deposits, owners). They
do not describe any real application.

This is shared context for human collaborators and AI tools alike.

## Structure

- **guidelines/**: how we write code. The contributor contract. Start with
  [architecture-principles.md](guidelines/architecture-principles.md).
- **adr/**: [architecture decision records](adr/). Why things are the way they are.
- **architecture/**: skeletons for the two documents an application keeps about itself
  ([overview](architecture/overview.md), [domain model](architecture/domain-model.md)). An
  application fills them in; this set does not.

## Conventions

- Every document starts with front-matter: `status`, `owner`, `last-updated`.
- One topic per file, ideally under ~200 lines.
- Link instead of duplicating: every fact has exactly one home.
- Diagrams are Mermaid code blocks.
- Requirements are numbered (`FR-x`, `NFR-x`) so code, tests and discussions can reference
  them.
- Current state goes in `architecture/`; reasoning and rejected alternatives go in an ADR.
  Never write history into an architecture document.
- **TODO** in a document means a team decision is pending, not a task someone forgot.

## Using this as agent input (AaaS)

**This set is chosen per archetype, not per run.** It is not an add-on to the golden path.
`aaas-app-template` is deliberately one project with minimal APIs, and its `AGENT.md`
contradicts this set in the places listed below. Given both, an agent follows the
repository it is working in. Finding 23 showed that when a brief and the template
disagree, the template wins. So these guidelines become agent input only through a
template that is laid out the way they describe:

1. A second template, e.g. `aaas-app-template-hexagonal`, with the four projects from
   [ADR-0005](adr/0005-adapters-as-separate-projects.md), the architecture tests from
   [testing-strategy.md](guidelines/testing-strategy.md) and the formatter from
   [ADR-0006](adr/0006-formatting-with-csharpier.md) already in CI.
2. This folder copied into that template's `docs/`, and its `AGENT.md` reduced to pointers:
   read `docs/guidelines/architecture-principles.md` first, then the rest before writing
   code.
3. The rules the compiler and CI enforce (project references, ArchUnitNET rules, CSharpier)
   are what make the guidelines real for an agent (D-17). The prose explains them; it does
   not enforce them.

Until that template exists, this is reference material for humans and for a bigger,
"for real" application.

### Where this set and the golden-path template disagree

| Topic | `aaas-app-template` today | This set |
|---|---|---|
| Layout | One project `src/App`, endpoints in `Program.cs` | `App.Core`, `App.Adapters.Web`, `App.Adapters.Persistence`, `App.Host` |
| Domain | EF Core entities are the model | Plain C# aggregates; separate EF entities and mappers ([ADR-0002](adr/0002-domain-free-of-ef-core.md)) |
| Tests | Must pass with no database | Domain and handler tests need none; persistence adapter tests run against a real Postgres |
| Formatting | `dotnet format --verify-no-changes` | CSharpier for layout, `dotnet format` for analyzers only |
| Assertions | xUnit `Assert` | AwesomeAssertions only |
| Migrations | EF migrations in `src/App` | EF migrations in `App.Adapters.Persistence` |

What both must keep, because the AaaS pipeline depends on it (context §4.3): the image
listens on `$PORT`, answers `/health` without a database, runs as non-root, and accepts
the single argument `migrate`. In this layout, `App.Host` owns all four.
