# ADR-0008: An inbound port's contract lives beside it in Ports/In

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

Two rules in [architecture-principles](../guidelines/architecture-principles.md) could not
both hold once one slice calls another: commands and queries lived in `Commands/` and
`Queries/` beside their handlers, while another slice may use only `Ports.In`. A slice
could not construct the command it needed to send. Found building the first cross-slice
call in learning-app (its ADR-0010).

## Decision

A port's whole contract lives in `<Slice>/Ports/In`: the interface, its command or query,
and its result types. `Commands/` and `Queries/` hold handlers only. This applies to every
port, not only those another slice uses today, so there is one pattern to copy.
Identifiers more than one slice needs live in `Common/Domain`.

## Alternatives considered

- **Allow other slices to use `Commands`/`Queries`**: the handlers' namespace becomes
  reachable, and nothing stops a slice constructing another slice's handler.
- **Only cross-slice contracts in `Ports/In`**: two places for the same kind of type,
  decided by who happens to call it.

## Consequences

- `Ports/In` reads as the slice's public API.
- A result carrying the slice's own domain types makes every caller depend on them; ports
  meant for other slices return primitives or `Common/Domain` types.
