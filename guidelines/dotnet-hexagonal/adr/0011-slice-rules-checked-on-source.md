# ADR-0011: Slice rules are checked on compiler-resolved source

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

The slice rules ran on IL through ArchUnitNET. In learning-app a deliberate violation inside
an async handler body passed: the compiler moves an async body into a generated state-machine
type, and ArchUnitNET does not attribute that type's dependencies to the class. Every handler
is async, so the rule never saw handler bodies.

## Decision

The rules about slices run on source: Roslyn compiles the core's files against the BCL and
resolves every name, inside async bodies, lambdas and local functions. On those references:

- a slice's `Domain` uses only itself and `Common.Domain`;
- a slice references another slice only to implement an interface from the other's `Domain`
  ([ADR-0008](0008-slices-meet-through-the-consumers-interfaces.md));
- the graph of slice-to-slice references has no cycle;
- a command handler depends on no query, a query handler on nothing that writes
  ([ADR-0012](0012-commands-and-queries-stay-apart.md)).

Each runs against a broken fixture as well; the fixture for every rule includes a violation
after an `await`. IL-level rules (ArchUnitNET) stay for what source does not show, such as
who calls `Hydrate`.

## Alternatives considered

- **Upgrade ArchUnitNET and hope**: not verified, and the next silent gap would look the same.
- **Read IL ourselves** over nested types: more code than the Roslyn check, same answer.

## Consequences

- A test compiles the core's sources (a couple of seconds).
- A reference reached without naming anything (through `var` and a return value, with no
  member access) is not seen; it reaches no behaviour without a member access, which is.
