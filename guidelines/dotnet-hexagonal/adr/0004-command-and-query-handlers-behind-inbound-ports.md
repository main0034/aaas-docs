# ADR-0004: Command and query handlers behind inbound ports

- **Status:** Accepted
- **Date:** 2026-09-28 (ported from the Java set, 2026-08-21)
- **Deciders:** Martin

## Context

[ADR-0001](0001-hexagonal-architecture-with-use-case-slices.md) committed to use-case
slicing, but did not fix the shape of the application layer. In the Java project the
first slice settled it: `commands`, `queries` and `port` folders directly inside the
slice, with no `application` wrapper. That layout is flat, and it uses the vocabulary the
team actually uses when talking about the work.

.NET adds one question the Java set did not face: whether to dispatch through a mediator
library (MediatR and its successors), which is the common .NET idiom for commands and
handlers.

## Decision

Each slice contains:

```
<Slice>/Domain           aggregates, value objects, invariants
<Slice>/Commands         XCommand + XCommandHandler   (writes)
<Slice>/Queries          XQuery + XQueryHandler       (reads)
<Slice>/Ports/In         the interfaces handlers implement
<Slice>/Ports/Out        the interfaces adapters implement
```

Writes and reads are separated at the folder and namespace level. Every handler
implements a small inbound port named after the operation (`ICreateUser`), so adapters
and other slices depend on an interface rather than on the handler type. One operation =
one command or query = one handler = one public method, `HandleAsync(...)`.

Callers resolve the inbound port from DI and call it directly. There is no mediator.

## Alternatives considered

- **Handlers with no inbound port**: one type fewer per operation, and the interface has
  exactly one implementation. Rejected: adapters would depend directly on handlers, and
  the test fake for a cross-slice call would have nothing to implement. The interface is
  also where the operation's contract is documented.
- **A mediator library** (`ISender.Send(command)`): familiar to .NET developers, and it
  gives a place for pipeline behaviours such as logging and validation. Rejected: it
  replaces a typed dependency (`IRecordDeposit`) with a runtime lookup, so "who handles
  this?" is no longer answered by the compiler or by "go to definition". It also adds a
  dependency to `App.Core`, and MediatR moved to a commercial licence in 2025.
  Cross-cutting behaviour goes in the web adapter or in decorators registered in
  `App.Host`.
- **Commands and queries sharing one folder**: rejected. The read side is allowed to
  bypass the domain and the write side is not, and that difference should be visible in
  the structure.

## Consequences

- Refines ADR-0001; it does not supersede it.
- Every operation costs three files: port, command or query, and handler. For trivial
  reads this will feel like ceremony, and the query side is where to watch for it.
- The read side has an explicit escape hatch: a query handler may return read models
  straight from a query port without loading an aggregate. Commands never may.
- "Go to definition" on a port leads to exactly one handler. For an AI agent reading the
  code, that is the whole reason not to use a mediator.
