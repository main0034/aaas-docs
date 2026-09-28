---
status: draft
owner: martin
last-updated: 2026-09-28
---

# Domain modelling

How we build the inside of the hexagon. Structure and the dependency rule are in
[architecture-principles.md](architecture-principles.md). The application's own entities
and invariants are in its [domain model](../architecture/domain-model.md), and the words
are in its glossary.

The domain is plain C#, testable with plain xUnit: no host, no `DbContext`, no
`WebApplicationFactory`.

## Aggregates

Aggregates are consistency boundaries: everything inside one is saved together, and its
invariants always hold. An application lists its boundaries in its domain model. The
illustrative accounts domain in these guidelines would have:

|  Aggregate  |     Root      |               Contains               |                          Why                           |
|-------------|---------------|--------------------------------------|--------------------------------------------------------|
| Account     | `Account`     | name, type, currency, archived flag  | Small, always loaded whole                             |
| Transaction | `Transaction` | one entry, references `AccountId`    | Unbounded in number, so not held inside `Account`      |
| Goal        | `Goal`        | target, period, linked account ids   | Linked accounts are references, not owned objects      |
| Group       | `Group`       | memberships                          | Bounded and small; membership rules must hold together |

References **across** aggregates are by identifier (`AccountId`), never by object and
never by EF navigation property. Rules that span aggregates (for example "a linked account
must belong to a group member") are checked in the handler, which is allowed to load both.

## Creation: `Create` and `Hydrate`

An aggregate comes into existence in two different situations, and we keep them visibly
apart ([ADR-0003](../adr/0003-split-aggregate-creation-into-create-and-hydrate.md)).
**Aggregates have no public constructors.**

|                         |           `Create(...)`            |          `Hydrate(...)`          |
|-------------------------|------------------------------------|----------------------------------|
| Means                   | Something happened in the business | We read stored state back        |
| Applies business rules  | Yes: creation-time invariants      | No: structural validity only     |
| Assigns identity        | Yes                                | No, the identity already exists  |
| Raises domain events    | Yes (when we add them)             | Never                            |
| Called by               | Handlers, test builders            | Persistence mappers, only        |

```csharp
public sealed class Account
{
    private Account(
        AccountId id, UserId owner, AccountType type, Currency currency, string name, bool archived)
    {
        Id = id;
        Owner = owner;
        Type = type;
        Currency = currency;
        Name = name;
        Archived = archived;
    }

    public AccountId Id { get; }
    public UserId Owner { get; }
    public AccountType Type { get; }
    public Currency Currency { get; }
    public string Name { get; private set; }
    public bool Archived { get; private set; }

    /// <summary>A user opens a new account. Creation-time rules live here.</summary>
    public static Account Create(
        AccountId id, UserId owner, AccountType type, Currency currency, string name)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        // A new account is never archived, and its currency is fixed from now on.
        return new Account(id, owner, type, currency, name.Trim(), archived: false);
        // later: Record(new AccountOpened(id, owner, type));
    }

    /// <summary>Reconstitution from stored state. No rules, no events, no defaults.</summary>
    public static Account Hydrate(AccountState state) =>
        // Deliberately accepts states Create would reject, e.g. Archived == true.
        new(state.Id, state.Owner, state.Type, state.Currency, state.Name, state.Archived);
}

public sealed record AccountState(
    AccountId Id, UserId Owner, AccountType Type, Currency Currency, string Name, bool Archived);
```

Why bother: with one constructor the two paths are indistinguishable. The difference gets
papered over with nullable parameters, or with "if the id is empty it must be new". That
works until we add domain events. At that point, reading a row from the database silently
raises an `AccountOpened` event. Naming the paths now makes that change local and safe.

Rules:

- Only persistence mappers call `Hydrate`. An ArchUnitNET test enforces it. Tests build
  aggregates with `Create` or a builder that uses it.
- `Hydrate` never applies defaults, timestamps or derived state. If stored data is missing
  something, fix it in a migration, not in `Hydrate`.
- `Hydrate` takes a state record (`AccountState`) from the start. It keeps the parameter
  list stable as fields are added, and the mapper builds one from the EF entity.
- When a field is added, both paths must change together. The compiler will say so, since
  the private constructor gains a parameter.

## Value objects

Value objects are immutable records with meaning and behaviour: `Money`, `Period`,
`DateRange`. Prefer them over bare `decimal`, `Guid` and `string`. A method taking
`(Money, AccountId)` cannot be called with the arguments swapped.

Use `sealed record` (a class) with a private constructor and a static factory that
validates. Do not use `record struct` for anything with an invariant: `default(Money)` and
`new Money()` always exist and skip the constructor. Expose state through get-only
properties, not `init`, so `with` cannot bypass validation either.

## Typed identifiers and names

Names and identifiers are never bare `string`s or `Guid`s. Then `Link(UserId, GroupId)`
cannot be called with the arguments swapped, and a `Username` cannot be stored where an
email belongs. In C# a record per type gives value equality and type distinctness for
free. A `UserId` and an `AccountId` holding the same `Guid` are different types, and
comparing them is a compile error. The Java original needed shared base classes for this;
here each type is a few lines:

```csharp
public sealed record UserId
{
    private UserId(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(value));
        Value = value;
    }

    public Guid Value { get; }

    public static UserId New() => new(Guid.CreateVersion7());   // a new identity, for User.Create
    public static UserId From(Guid value) => new(value);         // an identity that already exists

    public override string ToString() => Value.ToString();
}

public sealed record Username
{
    private Username(string value) => Value = value;

    public string Value { get; }

    public static Username From(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        if (normalized.Length > 50)
            throw new ArgumentException("Username is longer than 50 characters", nameof(value));
        return new Username(normalized);
    }

    public override string ToString() => Value;
}
```

- Types are `sealed`, have a private constructor and static factories. They are never
  `record struct`, for the `default` reason above.
- `New()` and `From(...)` are the identifier's version of the aggregate's `Create` and
  `Hydrate` split. Minting an identity is not the same act as accepting one that exists.
- New identifiers are UUIDv7 (`Guid.CreateVersion7()`). They are time-ordered, which keeps
  Postgres primary-key indexes compact. Do not rely on `Guid.CompareTo` matching Postgres
  `ORDER BY id`. Sort by a real timestamp when order matters.
- Normalization (trimming, lower-casing an email) happens in the factory *before* the
  constructor. Validation happens on the normalized value. String equality is ordinal and
  case-sensitive, so a type that must compare case-insensitively normalizes case in its
  factory.
- Because the persistence adapter has its own EF entities
  ([ADR-0002](../adr/0002-domain-free-of-ef-core.md)), those entities hold plain `Guid` and
  `string` columns. The mapper converts, and no EF value converters are needed.
- **TODO:** if the number of typed primitives grows past a dozen, evaluate a source
  generator (Vogen, StronglyTypedId) against the hand-written records. Record the choice
  as an ADR.

## Closed sets of alternatives

C# has no sealed interfaces or discriminated unions yet. For a closed set such as
`GoalProgress` = met, missed or not started, use an abstract record with a private
constructor and nested sealed records, and consume it with a `switch` expression:

```csharp
public abstract record GoalProgress
{
    private GoalProgress() { }

    public sealed record Met(Money Achieved) : GoalProgress;
    public sealed record Missed(Money Achieved, Money Shortfall) : GoalProgress;
    public sealed record NotStarted : GoalProgress;
}
```

The private constructor keeps other types from extending it in ordinary code. It is not
watertight, because the record's generated copy constructor is `protected`, so a code review
still guards the set. Revisit when C# ships union types.

## Behaviour, not data

**No anemic domain model.** If `Account` is only properties, and every rule lives in a
handler, we have layered code wearing a hexagon's clothes. The test: can you read `Account`
and learn what an account is allowed to do? `account.Deposit(...)`, `account.Archive()`,
`account.AssertOwnedBy(...)`. Not `account.Archived = true`.

## Invariants

The invariants listed in the application's domain model are enforced in constructors and
factories, so an invalid object cannot exist. They are not validated after the fact.
Nullable reference types are on and warnings are errors, so "this cannot be null" is
checked by the compiler rather than by a comment.

## Domain errors

Domain errors are domain exceptions (`AccountArchived`, `AccountNotFound`) deriving from
a common `DomainException`. The web adapter translates them to HTTP status codes (see
[coding-standards.md](coding-standards.md)). The domain never mentions 404 or 400.
