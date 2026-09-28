---
status: draft
owner: martin
last-updated: 2026-09-28
---

# Coding standards

Structure is covered in [architecture-principles.md](architecture-principles.md). This
document is about the code itself. When in doubt, follow the surrounding code, and prefer
boring and obvious over clever.

## Language level and build

C# on .NET 10, with these properties in `Directory.Build.props` for every project:

```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
<AnalysisLevel>latest-recommended</AnalysisLevel>
<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
```

Package versions are pinned centrally in `Directory.Packages.props` (Central Package
Management), with lock files (`packages.lock.json`) and `dotnet restore --locked-mode` in
CI.

Use the modern constructs where they make code plainer:

- **Records** for value objects, commands, queries, results and read models.
- **Primary constructors** for handlers and adapters, whose parameters are dependencies.
  Not for aggregates or value objects, which need a validating constructor body.
- **Switch expressions and pattern matching** over closed sets (see
  [domain-modelling.md](domain-modelling.md#closed-sets-of-alternatives)), instead of enums
  plus `switch` on a type field.
- **`var`** where the type is obvious from the right-hand side, not to hide it.
- **File-scoped namespaces**, one type per file, file named after the type.

## Naming

- Use glossary words exactly: `Valuation`, not `Snapshot` or `PortfolioValue`.
- Inbound ports are imperative verb phrases with the interface prefix: `IRecordDeposit`,
  `IArchiveAccount`, `IGetUser`. The command or query and the handler drop the prefix and
  add the suffix: `RecordDepositCommand`, `RecordDepositCommandHandler`. The single public
  method is `HandleAsync(...)`.
- Interfaces take the `I` prefix. This deviates from the Java original, which banned
  Hungarian-style prefixes. In .NET the prefix is the platform convention, and the
  analyzers (CA1715) expect it. Implementations never take an `Impl` suffix.
- Aggregate factories are always `Create(...)` and `Hydrate(...)`, never `Of`, `From`,
  `New` or a public constructor (see [domain-modelling.md](domain-modelling.md)). Value
  objects and typed identifiers, which have no lifecycle, use `From(...)`, and identifiers
  add `New()`: `Username.From("martin")`, `UserId.New()`.
- Outbound ports are named for the need in domain terms: `IUsers`, `IAccounts`,
  `IUnitOfWork`. They are never named after the technology behind them (`IEfUserRepository`).
- Async methods end in `Async` and take a `CancellationToken` as the last parameter, named
  `ct`, passed all the way down.
- Booleans read as predicates: `Archived`, `IsOwnedBy(...)`.
- No abbreviations (`acc`, `txn`, `val`).
- Test methods state the rule in words: `Deposit_on_archived_account_is_rejected`. Assert
  with AwesomeAssertions (see [testing-strategy.md](testing-strategy.md)).

## Nulls

- Nullable reference types are on, and warnings are errors. A `T` means never null; a `T?`
  means absence is a legitimate answer.
- `T?` as a **return type** for lookups only (`Task<Account?> ByIdAsync(...)`). Parameters
  and properties in the domain are non-nullable unless absence has business meaning (an
  optional `Note`).
- The `!` null-forgiving operator does not appear in `App.Core`. Outside it, every `!`
  carries a comment saying why the compiler is wrong.
- Empty collections, never `null` collections. Expose `IReadOnlyList<T>`, never a mutable
  `List<T>` field.

## Money and time

- All monetary values are `Money`: a `decimal` amount plus an ISO-4217 currency. A bare
  `decimal` appears only inside `Money`, and `double` or `float` never appear near money.
- Arithmetic on `Money` fails fast on mixed currencies.
- Dates a user chose are `DateOnly` and mean a calendar day in the user's terms.
  Timestamps the system generated are `DateTimeOffset` in UTC. `DateTime` does not cross a
  boundary.
- The current time comes from an injected `TimeProvider` (`timeProvider.GetUtcNow()`),
  never from `DateTime.Now` or `DateTimeOffset.UtcNow`.

## Errors

- Domain rule violations throw domain exceptions named after the rule (`AccountArchived`,
  `DuplicateValuationForDate`), deriving from a common `DomainException`.
- Not-found is a domain exception too (`AccountNotFound`). The domain never mentions HTTP.
- The web adapter has one `IExceptionHandler` that maps exception types to status codes
  and an RFC 9457 `ProblemDetails` body. No `try/catch` translating errors inside handlers
  or endpoints.
- Never catch an exception to log it and rethrow, and never `catch (Exception)` outside
  that one handler.

## ASP.NET Core and the host

- Endpoints are minimal APIs, grouped per slice (`app.MapGroup("/api/accounts")`) in the
  web adapter. Each endpoint receives an inbound port and a request DTO, calls
  `HandleAsync`, and maps the result to a response DTO. It contains no logic beyond that.
- Constructor injection only, registrations in `App.Host`, lifetimes explicit (`Scoped`
  for handlers and adapters that touch the `DbContext`).
- Configuration binds to options records through the options pattern, with
  `ValidateDataAnnotations()` and `ValidateOnStart()`. No `IConfiguration["key"]` lookups
  scattered through the code.
- `appsettings.json` plus one file per environment. **No secrets in the repository, ever.**
  Prefer no secrets at all: managed identity over connection strings with passwords.

## Persistence

- Persistence code lives in `App.Adapters.Persistence`: the `DbContext`, EF entity classes,
  their `IEntityTypeConfiguration<T>` mappings, mappers, and migrations. None of it
  appears in `App.Core`.
- Every schema change is an EF Core migration, generated by
  `dotnet ef migrations add <Name> --project src/App.Adapters.Persistence`, never
  hand-written DDL.
- **Migrations are append-only.** Never edit a migration that has been merged; write a new
  one. Prefer expand-only changes: add, never drop or rename in the same release.
- The model is never created from code at run time: no `EnsureCreated()` outside tests.
  Migrations are applied by an explicit step (the host's `migrate` argument), not on
  startup.
- Table and column names are `snake_case` (e.g. `UseSnakeCaseNamingConvention()` from
  `EFCore.NamingConventions`, or explicit names). Watch out for SQL reserved words: a
  `group` concept needs a table such as `saving_group`.
- An index is added in the same migration as the query that needs it.

## API conventions

Until the application's `docs/architecture/api.md` exists:

- Resources are plural nouns: `/api/accounts`, `/api/accounts/{id}/transactions`.
- JSON is `camelCase` (the `System.Text.Json` web default). Dates are ISO-8601. Money is an
  object `{"amount": "1000.00", "currency": "SEK"}`, with the amount as a string to avoid
  float rounding in clients.
- Request and response DTOs live in the web adapter, separate from commands and results.
  They are not reused across endpoints just because the fields match today.

## Formatting and hygiene

- Formatting is not a matter of taste here. [CSharpier](https://csharpier.com) formats the
  C# sources and has almost no settings. `dotnet csharpier check .` runs in CI, so
  unformatted code fails the build
  ([ADR-0006](../adr/0006-formatting-with-csharpier.md)).

  ```
  dotnet tool restore
  dotnet csharpier format .     # reformat everything
  dotnet csharpier check .      # what CI does
  ```

  Install the CSharpier plugin for Rider or Visual Studio and enable format-on-save, so the
  IDE and CI agree and the check never fires.

- `dotnet format` still runs in CI, but only for what CSharpier does not do: analyzer and
  code-style diagnostics (`dotnet format analyzers --verify-no-changes`,
  `dotnet format style --verify-no-changes`). Never `dotnet format whitespace`: two
  formatters that disagree fail the build in turns.
- No commented-out code, and no `TODO` without a name and an issue reference.
- No unused package references, and no `Console.WriteLine`. Use `ILogger<T>`.
- Log at the adapter boundary, not inside the domain. Never log personal or financial
  data.
