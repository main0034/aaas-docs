# Next session

Written 20 September 2026 at the end of the Phase 4a session. Replaced wholesale at the end
of every session — this file is intent, not history. What actually happened lives in
`FINDINGS.md`, where things stand lives in `STATUS.md`.

---

## Topic

**OQ-13 — choose and install the migration tool.**

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Two things depend on it, and nothing else does:

- `agent/create-app.md` is written but **uncommitted on purpose**, because it instructs the
  agent to put schema changes through versioned migrations and the scaffold has no migration
  tool. Shipping a runbook that promises a mechanism that does not exist is how an agent ends
  up inventing one.
- Phase 7 (code generation) cannot start until the scaffold can evolve a schema without
  losing data. The agent will change the data model repeatedly and `POC-PLAN.md` §4.3 is
  explicit that this must not be ad-hoc DDL.

Phase 4c (chaining create-app and create-deployment) sits behind this too, since it runs
`create-app.md`.

## Read first, in this order

| File | Why |
|---|---|
| `STATUS.md` | Current position. Phase 4a is proven; check nothing has changed since. |
| `FINDINGS.md` finding 14 | What the first agent run taught us, including the cost shape. |
| `aaas-app-template/AGENT.md` | The conventions any migration tool has to live inside. |
| `aaas-app-template/app/db.py` | How the connection is made — Entra token at connect time, no password. This constrains the tool choice more than anything else. |
| `aaas-deployments/agent/create-app.md` | The runbook that is waiting on this decision. |

## State to verify before starting

Don't trust this file on any of these — check:

- Is `agent/create-deployment.md` (the `--body-file` fix) pushed? If not, the next agent run
  repeats a refusal that is already fixed locally.
- Is `aaas-agent` pushed? It was four commits ahead at the end of the last session.
- Is PR #7 still open, and is infrastructure still destroyed? Nothing should be costing money.

## The work

1. **Establish the constraint that actually decides this.** The app authenticates to Postgres
   with a managed identity and a short-lived Entra token fetched at connect time — there is
   no password and no `DATABASE_URL`, deliberately (finding 11). Most migration tools expect
   a connection string. Whichever tool is chosen has to accept a connection created by the
   application's own code, or be able to fetch the token itself. **Check this before
   comparing features; it eliminates candidates faster than anything else.**

2. **Decide where migrations run.** Three options, and they have different failure modes:
   at container start-up (simple, but every replica races and a failed migration becomes a
   failed deploy), as a separate CI step before the image is deployed (clean, needs network
   access to a private-networked database), or as a one-off job. The private networking from
   Phase 1 makes the CI option harder than it looks — the database has no public endpoint.

3. **Add the tool to `aaas-app-template`** with one real migration, so the template is still
   immediately buildable and deployable as-is.

4. **Update `AGENT.md`** with the convention: where migration files live, how they are named,
   what the agent must never do (edit an applied migration).

5. **Then commit `create-app.md`** — it goes straight to master, because the `guardrails` job
   fails any PR touching `agent/`.

## Decisions I will need from you

- Which tool. My starting view is Alembic, on the grounds that it is the default for a
  Python/SQLAlchemy-adjacent stack and the agent will have seen the most of it — but the
  template uses raw `asyncpg` with no ORM, and Alembic without SQLAlchemy models is mostly
  hand-written SQL in a versioning wrapper. A plain SQL migration runner may fit better.
  Expect me to argue this rather than assume it.
- Where migrations run (step 2). This is a real architectural choice, not a detail.

## Done when

- The template has a migration tool, one migration, and still builds and passes CI.
- `AGENT.md` states the convention.
- `create-app.md` is committed and pushed, no longer promising something imaginary.
- A decision row is in `AaaS-context.md` §7 and OQ-13 is deleted from §8.

## Explicitly not this session

- Phase 4c. It comes after.
- Anything commercial — parked under D-19.
- Revisiting Azure. Closed under D-13.

## Carried over

- **Decide whether `gh pr close` should be allowed.** The policy currently refuses it while
  `PROMPT.md` only forbids merging, so the agent offered to close PR #7 and would have been
  refused. Small, but it is a mismatch between what the agent believes it can do and what the
  gate allows, which is the class of thing the refusal log exists to eliminate.
- **Resume is not implemented.** `session_id` is now recorded; the container discards the
  session store (`--rm`) and keys sessions by a per-run working directory. Documented in
  `aaas-agent/README.md`. Nothing needs it yet.
- **`.terraform.lock.hcl` does not exist.** Each CI run resolves azurerm afresh within
  `~> 4.20`, so provider versions can drift between runs. One `terraform init` away.
- **Offer to turn this hand-off into a skill**, so preparing it does not depend on remembering.
