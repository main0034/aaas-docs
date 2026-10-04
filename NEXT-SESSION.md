# Next session

Written 4 October 2026 at the end of roadmap step 2a. Replaced wholesale at the end of every session —
this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things stand
lives in `STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 2b: the spec-tester's tests as a required check the builder cannot read.** One change
runs end to end: brief → spec-tester writes tests → tests stored where the builder cannot read them →
builder opens a PR → CI runs the hidden tests as a required `acceptance` check → red goes to a fix
round, green is merge-ready. Then decide whether one class of change auto-merges.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Finding 26: the builder's green was wrong on the largest brief. Finding 27: a spec-tester session that
never sees the code caught that defect and 15/17 planted bugs, with no false reds, for $0.42-0.77. The
tests work. What is missing is the plumbing that makes them a gate instead of something the operator
runs by hand. Without it, OQ-5 (auto-merge) cannot move.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` findings 26-27 | The numbers, the shared blind spot, the caveats |
| `aaas-agent/README.md` + `harness/main.py` (`write-acceptance`, `fix_forward`) | Where the two runs and the fix loop are wired |
| `aaas-app-demo/.github/workflows/ci.yml` + `release.yml` | The `test` job the new job sits beside; how `release.yml` already mints an `aaas-bot` App token |
| `aaas-agent/acceptance/` | `AcceptanceBase.cs`, the operator's tests, the spec-tester's, and the scorer |

## Before the session - Martin

1. **Create a private repo `aaas-acceptance`** and install the `aaas-bot` App on it with Contents: read.
   This can't be done from my side: the operator token cannot create repos or install Apps.
2. Be ready to **push a `ci.yml` change to `aaas-app-demo`** yourself. Nothing I or the agent hold has
   the Workflows permission, on purpose (finding 19).

## State to verify before starting

- `aaas-agent` `master` has #3 (`write-acceptance`); `aaas-deployments` `master` has `a25e396` (runbook + the set-member rule).
- `aaas-acceptance` exists, is private, and the `aaas-bot` App can read it (mint a token in a dry run).
- `aaas-app-demo` PRs #14-16 are still open (unless Martin decided otherwise). `rg-demo-dev` does not exist.
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` in any repo's `.git`. `gh` works in the linked shell.

## The work

1. **Change id.** The harness gives each change an id (the run id of the spec-tester run). It commits that
   run's test file to `aaas-acceptance/<app-repo>/<id>/` with the operator token. The builder run is told
   the id and puts `Acceptance: <id>` in the PR body. The harness passes it; the agent does not choose it.
2. **`acceptance` job in `ci.yml`** (Martin pushes it): mints an App token, checks out
   `aaas-acceptance/<repo>/<id>` (the id is read from the PR body), copies it with `AcceptanceBase.cs`
   into `tests/App.Tests/Acceptance/`, and runs `--filter-namespace Acceptance` against the job's
   Postgres. A missing id or missing directory is **red**, not skipped. The job is added to the ruleset's
   required checks.
3. **`--task create-change`** in the harness: spec-tester run, then builder run, then fix-forward. A red
   `acceptance` check feeds its trimmed log to the fix round like any other check.
4. **Run it once on a new brief**, written for this session and not seen by either role before. Then
   **re-run `projects`** to see #16's miss go red in CI and get fixed by a fix round.
5. **One finding**, and a one-line OQ-5 position: which class of change, if any, merges without a human.

## Decisions I will need from you

- **Where the tests live.** Expect me to argue: a private `aaas-acceptance` repo read by the `aaas-bot` App
  token that `release.yml` already uses. That adds no new secret (D-14 holds). The alternatives were
  rejected: an encrypted file in the app repo still means a key to manage, and running the tests in the
  harness would mean the gate isn't a check.
- **What the fix round sees.** Expect me to argue: the failing test's name and assertion message, as with
  any red check. The tests come from the spec, so fixing the code to pass them is fixing it to meet the
  spec. The fix round must not edit the hidden tests, and it can't: they are not in the repo.
- **Threat model, stated once.** The builder's code runs in the same CI job as the hidden tests, so a
  hostile builder could print them. Expect me to argue: accept it. The point is independence, not
  secrecy from an adversary, and the builder has no reason to look. Record it as a limit of the design.
- **Which change, if any, auto-merges.** Expect me to argue: none this session. Turn it on after step 4
  if the `acceptance` check behaves, and only for changes that add routes without a migration. Changes
  with a migration stay gated (OQ-5, OQ-7).

## Done when

- One change runs brief → spec-tester → hidden tests in `aaas-acceptance` → builder PR → required
  `acceptance` check → green, without the operator touching the tests.
- `projects` re-run: the hidden check goes red on the trimmed-name rule (or the builder gets it right)
  and the outcome is recorded.
- A finding, and a one-line OQ-5 position in `AaaS-context.md`.

## Explicitly not this session

- Auto-merge on a migration-bearing change. Deployment (`apply`). A new app from nothing (OQ-15).
- OQ-21, the `workload_profile_name` diff, anything commercial (D-19).

## Carried over

- **Spec-tester blind spot:** both `projects` runs missed "delete only when no items" with done items.
  The rule is now in the runbook (`a25e396`) and not yet re-measured.
- **Untracked in `aaas-agent`:** `acceptance/` (operator tests, `spec-tester/`, `score/`), briefs
  `item-upcoming.md`, `item-tags.md`, `projects.md`, `item-due.md`, `item-priority.md`,
  `notebook-fix-cli-missing.md`, and the `.gitignore` line.
- **Run drivers:** `phase7.command` is current. `session-1001.command`, `phase6.command`, `notebook-*.command`
  and their log folders can go.
- **Policy false positives:** pipes after `dotnet` (finding 26), the `nuget` path, heredoc `fix:`. The
  spec-tester had none in five runs.
- **The permanent `workload_profile_name` diff**; **the operator token is admin**; **OQ-21**; **`gh pr close`
  refused by policy**; **no `.terraform.lock.hcl`**; **no ruleset on `aaas-deployments`**; **a capped run
  loses its work** (finding 22).
- **Merged branches to delete:** `test/endpoint-tests` (template, demo), `feat/priority-list` (demo),
  `feat/write-acceptance` (agent).
