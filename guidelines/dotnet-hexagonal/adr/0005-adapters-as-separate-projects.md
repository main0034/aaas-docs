# ADR-0005: Adapters live in separate projects

- **Status:** Accepted
- **Date:** 2026-09-28 (ported from the Java set's "separate Maven modules", 2026-08-21)
- **Deciders:** Martin

## Context

ADRs [0001](0001-hexagonal-architecture-with-use-case-slices.md) and
[0004](0004-command-and-query-handlers-behind-inbound-ports.md) settle the inside of the
hexagon. They leave adapters with two possible homes: folders within each slice in a
single project, or separate projects referencing a core project.

The dependency rule is the single most important structural rule in this codebase. In a
single ASP.NET Core project the web SDK and EF Core are on every file's reference list.
Nothing stops a contributor, or an AI tool, from adding `using
Microsoft.EntityFrameworkCore;` to a domain class. An architecture test catches it, but
only once someone runs the tests, and only for the patterns the test happens to describe.

## Decision

The solution is split into projects. The core project contains the feature slices
(`Domain`, `Commands`, `Queries`, `Ports/In`, `Ports/Out`). It uses the plain
`Microsoft.NET.Sdk`, with no `Microsoft.AspNetCore.App` framework reference and no
package references at all. Each adapter is its own project referencing core, and a thin
host project assembles them.

|           Project           |                            Contains                             |        References         |
|-----------------------------|-----------------------------------------------------------------|---------------------------|
| `App.Core`                  | all feature slices: domain, commands, queries, ports            | the BCL only              |
| `App.Adapters.Web`          | minimal API endpoint groups, request/response DTOs, error mapping | `App.Core`, ASP.NET Core |
| `App.Adapters.Persistence`  | `DbContext`, EF entities and configurations, mappers, migrations | `App.Core`, EF Core, Npgsql |
| `App.Host`                  | `Program.cs`, configuration, DI registrations, the `migrate` entry point | all of the above |

Test projects mirror them: `App.Core.Tests` references `App.Core` only.

Slices are folders inside `App.Core`, not projects of their own. Splitting per slice
would multiply projects without protecting anything the architecture tests cannot
already see.

## Alternatives considered

- **Adapter folders inside each slice, one project**: the simplest build, with everything
  for one feature in one place. It is what the AaaS golden-path template does, deliberately,
  for small generated applications. Rejected here: it leaves the dependency rule as a
  convention enforced only by a test.
- **One project per slice as well**: compile-time slice isolation. Rejected for now: the
  slice boundaries are not settled early in a project, and moving code between projects
  is expensive. ArchUnitNET covers slice isolation until they are.

## Consequences

- **The dependency rule becomes a compile error.** `App.Core` has no ASP.NET Core or EF
  Core on its dependency graph, so a domain class *cannot* use `DbContext`. The mistake is
  impossible rather than merely detectable. This is the whole point of the decision.
- The architecture tests shrink to what the compiler still cannot see. They also guard
  against the one way around this decision: someone adding a package reference to
  `App.Core`.
- Build ceremony: a solution with four source projects and their test projects, and a
  `Directory.Build.props` to keep them consistent. The Java version paid for this with a
  parent pom and a rebuild of `core` before adapters saw a change. In .NET, project
  references rebuild incrementally, so the cost is mostly navigation.
- A feature no longer lives in one folder. Adding an endpoint touches `App.Core` and
  `App.Adapters.Web`, and the reviewer follows a port between them.
- The container image is built from `App.Host`. That project owns the image contract the
  AaaS pipeline relies on: `$PORT`, `/health` without a database, non-root, and the
  `migrate` argument.
