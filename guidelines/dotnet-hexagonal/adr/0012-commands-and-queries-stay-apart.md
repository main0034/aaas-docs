# ADR-0012: Commands and queries stay apart

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin

## Context

The guidelines separated writes (`Commands`) from reads (`Queries`) and let query handlers
bypass the domain (CQRS-lite), but said nothing about how the two may use each other. In
learning-app a command handler called three queries, one of them a domain operation filed as a
query because it did not write.

## Decision

- **A query never writes**: no `IUnitOfWork`, no repository method that adds, changes or
  removes. It returns data for a reader, and may come from a read model rather than the domain.
- **A command never depends on a query**, in its own slice or another. It decides through its
  slice's domain and repositories, and through interfaces in its `Domain` that other slices
  implement ([ADR-0008](0008-slices-meet-through-the-consumers-interfaces.md)). A read model is
  shaped for a screen and changes with it; business decisions do not hang on it.
- **A command returns its own outcome**: an identifier, or the result of what it did (right or
  wrong), never a read model. A page that needs to show more asks a query afterwards.
- Checked on source ([ADR-0011](0011-slice-rules-checked-on-source.md)).

## Alternatives considered

- **Commands may use queries** (common in modular monoliths): one database and one transaction
  mean no staleness here, but the coupling of rules to screen-shaped data remains, and an agent
  would reach for whichever port exists.
- **A third kind, "services", for domain operations offered to other slices**: works, at the
  cost of a third folder and a third set of rules; consumer-owned interfaces need neither.

## Consequences

- CQS (Meyer) holds per operation, CQRS-lite per slice.
- A command that used to return "the new state" returns less; the page makes one more call.
