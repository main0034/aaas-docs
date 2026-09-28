# ADR-0001: Hexagonal architecture with use-case slices

- **Status:** Accepted
- **Date:** 2026-09-28 (ported from the Java set, 2026-08-21)
- **Deciders:** Martin

## Context

The applications this set is for get their value from their rules, not their plumbing:
how a balance is derived, when a period counts as met, who may see what. Those rules must
stay clear and testable while several collaborators, and AI tools, work on the code in
parallel.

The default shape of an ASP.NET Core application is controller → service → repository,
with EF Core entities serving as the model. It is familiar, and it tends to produce
services that accumulate a method per endpoint and entities that are only data. That puts
the rules in the least testable place, and makes parallel work collide in large shared
classes.

## Decision

We structure the code as a hexagon. It has a framework-free domain, an application layer
of named commands and queries behind inbound ports, and adapters implementing outbound
ports. Code is sliced by feature first (`Accounts`, `Goals`, `Users`), with the same
hexagon shape inside each slice. DDD tactical patterns apply to the domain: aggregates,
value objects, and invariants in constructors. SOLID guides the seams, with dependency
inversion as the load-bearing principle.

The details are in
[guidelines/architecture-principles.md](../guidelines/architecture-principles.md).

## Alternatives considered

- **Conventional layered architecture** (controllers, services, EF entities as the model):
  the simplest to start, and what most .NET samples show. Rejected because it puts rules
  in services over an anemic model, and because slices collide in shared service classes.
- **Full CQRS with separate read and write stores, or a mediator library for everything**:
  a good fit for read-heavy dashboards, but disproportionate to the size of these
  applications. We keep a narrow escape hatch instead: read-only query ports returning
  read models. We call handlers through their ports, not through a mediator
  (see [ADR-0004](0004-command-and-query-handlers-behind-inbound-ports.md)).
- **Vertical slice architecture without a shared domain**: fewer layers, and each feature
  is self-contained. Rejected because the rules that matter span features, and they need
  one home: the aggregate.

## Consequences

- Rules become unit-testable without a host or a database, and the list of handler types
  documents what the application does.
- More types and explicit mapping. A single "record a deposit" feature touches a port, a
  command, a handler, an aggregate method, an EF entity and a mapper. This is a real cost,
  and it must not become ceremony for its own sake.
- Contributors need to learn the pattern before their first change. The guidelines carry
  that burden and must stay readable.
- For an AI agent, the structure is an advantage and a risk. It is an advantage because
  the place for each kind of code is fixed and nameable. It is a risk because an agent
  will imitate whatever shape the existing code has, so the first slices set the pattern
  for all the others. Architecture tests are what keep it from drifting.
