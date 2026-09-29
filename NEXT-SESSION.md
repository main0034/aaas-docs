# Next session

Written 29 September 2026 at the end of the Phase 6 session. Replaced wholesale at the end of
every session — this file is intent, not history. What actually happened lives in
`FINDINGS.md`, where things stand lives in `STATUS.md`.

---

## Topic

**Tests that prove behaviour.** Give the app's tests the Postgres that CI already runs for
migrations, and change `AGENT.md` so that every new or changed route gets a test that calls the
endpoint against a real database and asserts which rows come back. Template first, then
`aaas-app-demo`, then one agent run to see whether the agent writes that kind of test unprompted.

Before that, **one clean create on `app-stack` v0.3.2** - the only unticked item in `POC-PLAN.md`
§9. It is a check, not the topic: if it fails, it becomes the topic.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Three runs in a row (findings 21, 23, 24) produced correct code with tests that could not have
caught it being wrong: 503 checks and in-memory `IQueryable` tests that compose the query
themselves. Briefs asking for endpoint tests did not help; `AGENT.md` says tests run without a
database, and the template wins. Until a green check means the endpoint returns the right rows,
automatic merge (OQ-5) is off the table and Phase 7 (open-ended code generation) would be judged by
tests that prove nothing.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` finding 24 | The Phase 6 run, the create race, the third round of weak tests. |
| `FINDINGS.md` findings 21 and 23, the test sections | What "weak" looked like each time, and why the brief lost. |
| `aaas-app-template/AGENT.md`, "Every new route gets a test" | The rule that has to change. |
| `aaas-app-template/.github/workflows/ci.yml` | `test` has no Postgres; `build` has `postgres:16` as a service. The fake-database guard. |

## Before the session - Martin

1. **Confirm the 29 September destroy is green.** It now verifies against Azure itself.
2. **Run `apply` from the GitHub UI** with `deployments/dev/demo`, on the destroyed stack. That is the
   v0.3.2 create proof, ~10 minutes; I read the run and the readiness gate at the start. Leave the
   stack up - the session's agent run deploys onto it as an update.

## State to verify before starting

- The `destroy` of 29 September is green, including the new **Verify against Azure** step.
- The `apply` Martin started: green, `Plan`/apply shows 14 created, **no "already exists"**, readiness
  gate green. The apply log should show `azurerm_container_app.this: Creating...` *after*
  `azurerm_postgresql_flexible_server_database.this: Creation complete`.
- `deployments/dev/demo/main.tf` pins `v0.3.2`; `imports.tf` does not exist.
- `aaas-app-demo` `master` = `63ec008` or later (due dates); the image pinned in tfvars matches.
- No `index.lock` / `HEAD.lock` in any repo's `.git`.

## The work

1. **Template CI:** a `postgres:16` service on the `test` job, its connection passed as an
   environment variable. Tests that need it read it; without it they are **skipped with a message**
   locally, never faked (the in-memory/SQLite guard stays).
2. **Template test fixture:** a `WebApplicationFactory` against that Postgres - migrations applied
   once, each test class in its own database or transaction, so tests cannot see each other's rows.
3. **`AGENT.md`:** replace "tests must pass with no database" with the new rule and one worked
   example (an endpoint test asserting the ids that come back, including one that must not).
4. **Port to `aaas-app-demo`**, and rewrite the `/items/overdue` and search tests that way as the
   proof that the fixture works - including a deliberately wrong ordering that the new test catches.
5. **One agent run** with a brief that does *not* mention tests. Done if it writes an endpoint test
   against Postgres on its own.

## Decisions I will need from you

- **Where the agent runs these tests.** The harness container has no Postgres and no Docker. Expect
  me to argue: it doesn't - it pushes, CI runs them, and fix-forward (D-23) handles red. Adding a
  Postgres to the harness is more moving parts than one extra fix round (~$0.38).
- **Every test against Postgres, or a category.** Expect me to argue: a category (`[Trait]`) for
  endpoint tests only; pure logic tests stay database-free and fast.
- **Isolation: database per test class vs transaction rollback.** Expect me to argue: database per
  class from a migrated template database - rollback breaks as soon as an endpoint commits itself.

## Done when

- The v0.3.2 create went green without help, and §9 is fully ticked.
- The template's CI runs endpoint tests against Postgres; a planted bug in the demo's endpoint
  (wrong ordering or filter) turns `test` red.
- One agent run, briefed without mentioning tests, produced an endpoint test that asserts rows.

## Explicitly not this session

- Automatic merge on green (OQ-5) - this session is what makes it discussable, not the decision.
- A new app from nothing (OQ-15). Fix-forward after a *deploy* failure (OQ-21). Anything commercial (D-19).
- The `workload_profile_name` diff (finding 24), unless it gets in the way.

## Carried over

- **The permanent in-place diff** on the Container App after every create (`workload_profile_name`
  "Consumption" → null, empty `args`/`command`). Harmless so far; likely fixed by setting it in the
  module.
- **The operator token is admin**, so the `aaas-app-demo` ruleset does not bind it.
- **OQ-21 — "immutable once merged" is the wrong rule.** Unchanged.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **`verify-isolation` cannot see token permissions.** A push dry-run could become a start-up check.
- **A policy false positive:** a `find` whose path contained `nuget` was refused as `dotnet nuget`.
- **A capped run still loses its work** (finding 22).
- **Untracked in `aaas-agent`:** `docs/` (README, adr, architecture, guidelines - including the ported
  .NET hexagonal set), four `notebook*` briefs, and a `.gitignore` line (`.idea`). Martin's to commit or
  discard.
- **Run drivers:** `phase6.command` is current. `phase4c.sh`, `notebook-agent.command` and
  `notebook-app.command` are superseded and can be deleted.
