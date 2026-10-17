# ADR-0004: Operations are an interface, a contract and a handler, kept together

- **Status:** Accepted
- **Date:** 2026-10-17
- **Deciders:** Martin (first decided 2026-08-21 in the Java set; rewritten 2026-10-17)

## Context

[ADR-0001](0001-hexagonal-architecture-with-use-case-slices.md) committed to use-case slicing
without fixing the shape of the application layer. The first layout put the interfaces in
`Ports/In` and `Ports/Out` folders, apart from the handlers that implement them and the
domain that needs them. Everything about one operation was spread over three folders, and
the folders said nothing a type name did not.

.NET adds one question: whether to dispatch through a mediator library (MediatR and its
successors), the common .NET idiom for commands and handlers.

## Decision

Each slice contains:

```
<Slice>/Domain     aggregates, value objects, invariants, and the outbound interfaces the
                   slice needs: IUserRepository, IContentStore, IGradeAnswer
<Slice>/Commands   per write:  ICreateUser, CreateUserCommand, CreateUserCommandHandler
<Slice>/Queries    per read:   IGetUser, GetUserQuery, UserView, GetUserQueryHandler
```

- **An operation is an interface, its command or query (and result), and its handler, in one
  folder.** The interface is the inbound port, named after the operation (`ICreateUser`).
  One operation = one command or query = one handler = one public method, `HandleAsync`.
- **Outbound interfaces live in the slice's `Domain`**: what the slice needs from outside,
  in its own words (Evans puts repository interfaces in the domain for the same reason).
  Adapters implement them; so may another slice ([ADR-0008](0008-slices-meet-through-the-consumers-interfaces.md)).
- There is no `Ports` folder. A port is a role, not a place: inbound ports are the
  interfaces in `Commands`/`Queries`, outbound ports the interfaces in `Domain`.
- Callers resolve the inbound interface from DI and call it directly. There is no mediator.

## Alternatives considered

- **`Ports/In` and `Ports/Out` folders** (the first version): one folder as the slice's public
  surface, but one operation in three places, and the boundary between slices drawn by a
  folder name rather than by what a type is.
- **Handlers with no interface**: one type fewer, but adapters would depend on handlers and a
  test fake would have nothing to implement.
- **A mediator library** (`ISender.Send(command)`): replaces a typed dependency with a runtime
  lookup, so "who handles this?" is no longer answered by "go to definition"; a dependency in
  `App.Core`; MediatR moved to a commercial licence in 2025. Cross-cutting behaviour goes in
  the web adapter or in decorators registered in `App.Host`.

## Consequences

- Every operation costs three or four files in one folder. "Go to definition" on an interface
  leads to exactly one handler, next to it.
- The slice's public surface is no longer one folder. What another slice may touch is a rule
  ([ADR-0008](0008-slices-meet-through-the-consumers-interfaces.md)), checked on source.
- Read and write stay apart at folder level, and they meet under the rules of
  [ADR-0012](0012-commands-and-queries-stay-apart.md).
