# ADR-0011: Slice rules are also checked on compiler-resolved source

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

The slice rules ran on IL through ArchUnitNET. In learning-app (its ADR-0011) a deliberate
violation inside an async handler body passed: the compiler moves an async body into a
generated state-machine type, and ArchUnitNET does not attribute that type's dependencies to
the class. Every handler is async, so the rule never saw handler bodies.

## Decision

The rules "a slice uses another only through its `Ports.In`" and "a domain uses only itself
and `Common.Domain`" also run on source: Roslyn compiles the core's files against the BCL and
resolves every name, inside async bodies and lambdas too. A fixture whose only violation sits
after an `await` proves the check bites. The IL rules stay.

## Alternatives considered

- **Upgrade ArchUnitNET and hope**: not verified, and the next silent gap would look the same.
- **Read IL ourselves** over nested types: more code than the Roslyn check, same answer.

## Consequences

- A test compiles the core's sources (a couple of seconds).
- Any IL rule is shown to fail on a violation inside an async method before it is trusted.
