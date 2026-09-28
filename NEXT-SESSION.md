# Next session

Written 28 September 2026 at the end of the Phase 5 session. Replaced wholesale at the end of
every session — this file is intent, not history. What actually happened lives in
`FINDINGS.md`, where things stand lives in `STATUS.md`.

---

## Topic

**Phase 6 — the demo.** One end-to-end run from a destroyed stack, recorded, with written notes on
what broke and what it implies (`POC-PLAN.md` §8). Plain-language request → app PR → CI (and a fix
round if CI goes red) → merge → image → deployment PR → create → readiness gate → the feature working
in a browser → destroy, verified. Then tick off `POC-PLAN.md` §9 honestly.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Phases 0–5 are proven separately, across runs that each needed some hand-holding. The demo is the
first time the whole chain has to work in one sitting, from nothing, while being watched — which is
exactly what finding 23's failed create says it may not yet do. Phase 7 (open-ended code
generation) should start from a chain that is known to run clean, not one that works on the second
attempt.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` finding 23 | Fix-forward numbers, the run leak, the `appdb` create failure and its import recovery. |
| `FINDINGS.md` findings 18 and 20 | The other Postgres-child failures, and why a green destroy proves nothing. |
| `POC-PLAN.md` §8 and §9 | What Phase 6 is, and the acceptance list it closes. |
| `aaas-agent/README.md`, "Fix-forward" | `--fix-rounds`, `--skip-local-checks`, what the run record shows. |

## Before the session - Martin

1. **Confirm the destroy removed everything:** `az group exists -n rg-demo-dev` → `false`. The
   28 September destroy was green, but its hostname still resolved minutes later.
2. **Push the post-destroy assertion** (the five lines finding 20 asked for) — it is under
   `.github/workflows/`, so only you can. Claude can prepare it as a patch at the start of the
   session, the way `phase5/patches/` worked.

## State to verify before starting

- **Infrastructure down:** `destroy` run `36462803976` green (28 September, 18:32Z) *and*
  `ca-demo-dev.bravemeadow-0aa9b7fc.swedencentral.azurecontainerapps.io` no longer resolves. The
  deployment still pins image `7f382fa`; schema `20260926142702_MarkItemDone`.
- `deployments/dev/demo/imports.tf` does **not** exist (removed by PR #19). If it is back, a create
  plan will try to import a database that does not exist.
- `aaas-agent` `master` includes the fix-forward work ([#2](https://github.com/main0034/aaas-agent/pull/2),
  `be1c938` or later), `uv run pytest -q` → 114 passed. `run.sh` mounts `runs/<id>`, not `runs/`.
- `aaas-app-demo` and `aaas-app-template` `master` both have the `no fake database in tests` step.
- No `index.lock` / `HEAD.lock` in any repo's `.git`.

## The work

1. **Make the create survivable before recording it.** Finding 23: the create failed on `appdb`
   "already exists". Options, cheapest first: (a) a `depends_on` in `app-stack` so the database is
   created after the SSL configuration and the AAD administrator, not in the same second (new tag
   `v0.3.1`; never move `v0.3.0`); (b) accept it and script the import recovery. Try (a) once with a
   create; it is the only change to infrastructure this session.
2. **Pick a brief the demo can show in a browser** in under a minute. A new feature on the items app.
3. **Run it once, recorded**, from the destroyed stack: prompt → running → destroy, each step timed
   from the run record and the Actions timestamps.
4. **Write the notes**: what broke, what it implies, and §9 of `POC-PLAN.md` ticked or not, each
   with the finding that proves it.

## Decisions I will need from you

- **What "recorded" means.** Expect me to argue: a screen recording of the browser at the start and
  end, with the middle as a timed log (the run record plus Actions timestamps) — 20+ minutes of
  pipeline is not watchable, and the log is what a reader can check.
- **Fix round in the demo or not.** Expect me to argue: run the normal runbook and show whatever
  happens; do **not** use `--skip-local-checks` in a demo, because it stages the failure. Finding 23
  is the evidence that fix-forward works.
- **`depends_on` in the module (a) vs scripted import (b).** Expect me to argue (a): it is a
  one-line ordering change that addresses the likely cause; (b) is a recovery, not a fix.

## Done when

- One uninterrupted run from a destroyed stack reaches a green readiness gate, with the feature
  shown working, and the destroy afterwards is verified against Azure, not just reported green.
- The notes exist, with timings, and `POC-PLAN.md` §9 is ticked with evidence.

## Explicitly not this session

- A new app from nothing — `gh repo create` from the template is provisioning (OQ-15).
- Automatic merge on green (OQ-5). Fix-forward after a *deploy* failure (OQ-21).
- Making tests prove behaviour against a real Postgres (below). Anything commercial (D-19).

## Carried over

- **Tests that prove nothing, again** (findings 21, 23): the brief asked for endpoint tests that show
  which items come back; `AGENT.md` says tests run without a database, so the agent wrote 503 checks
  and `IQueryable` tests. The template has to change - CI's Postgres available to tests, or a test
  category that runs there - before a green check means much. Candidate for the session after the demo.
- **The operator token is admin**, so the `aaas-app-demo` ruleset does not bind it.
- **OQ-21 — "immutable once merged" is the wrong rule.** Unchanged.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **`verify-isolation` cannot see token permissions.** The push dry-run used on 28 September
  (write to `aaas-deployments` + the app, refused on `aaas-agent` and `aaas-docs`) could become a
  start-up check.
- **A policy false positive:** a `find` whose path contained `nuget` was refused as `dotnet nuget`.
- **A capped run still loses its work** (finding 22); a fix round that hits `max_turns` now at least
  says so on its last line.
- **Run drivers:** `phase5/`, `phase4c.sh` and `notebook-agent.command` were deleted on 28 September.
  Phase 6 needs a new one-click driver in the same shape (build, run, watch for `RERUN`/`STOP`).
- **.NET hexagonal guidelines** (`guidelines/dotnet-hexagonal/`, ported 28 September from Martin's
  Java set). Reference material now. It becomes agent input through a second template that
  ships it, not through a flag on golden-path runs; its README lists every conflict with
  `aaas-app-template`. Not for Phase 6 (D-3: one golden path first).
