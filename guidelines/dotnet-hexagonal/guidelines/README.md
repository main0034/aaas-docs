---
status: draft
owner: martin
last-updated: 2026-09-28
---

# Guidelines

The contributor contract. If a change conflicts with a guideline, change the code or change
the guideline in the same pull request. Never leave them disagreeing.

- [architecture-principles.md](architecture-principles.md): hexagonal architecture,
  use-case slicing and SOLID, applied concretely. **Read this first.**
- [domain-modelling.md](domain-modelling.md): DDD inside the hexagon. Aggregates, value
  objects, typed identifiers, invariants, and the `Create`/`Hydrate` split.
- [coding-standards.md](coding-standards.md): C# and ASP.NET Core conventions, naming,
  nullability, money and time
- [testing-strategy.md](testing-strategy.md): what to test, where, and with what
- [git-workflow.md](git-workflow.md): branches, commits, pull requests

Sections marked **TODO** need a team decision. Bring them to a discussion rather than
deciding alone in a pull request.
