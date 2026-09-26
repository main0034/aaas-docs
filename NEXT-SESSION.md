# Next session

Written 26 September 2026 at the end of the Phase 4c session. Replaced wholesale at the end of
every session — this file is intent, not history. What actually happened lives in
`FINDINGS.md`, where things stand lives in `STATUS.md`.

---

## Topic

**Phase 5 — fix-forward.** The agent opens a PR, a check fails, and the agent reads the actual
failure and corrects its own PR, within the two rounds `create-app.md` section 6 allows. One run
that goes red and comes back green without a human, timed and costed.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Phase 4c proved the happy path: prompt → running app in 8m 23s for an update (finding 21). Both
runs were green on first push, so the runbook's "If CI fails" section has never executed. It is
also the loop a real user lives in — and finding 21 showed green is not the same as correct, so
the product will depend on how well the agent reacts to *red*. Phase 6 (the recorded demo) and
Phase 7 sit on top of this.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` finding 21 | The timings, the three runbook fixes, the weak tests, the unenforced merge. |
| `aaas-deployments/agent/create-app.md` section 6 | The fix-forward instructions nobody has run. |
| `aaas-agent/harness/main.py` | `--non-interactive` gives the agent exactly one exchange and ends at the PR. |
| `FINDINGS.md` finding 14, "The cost is context" | A follow-up costs as much as the original work. Decides where the loop should live. |

## Before the session - Martin, in the GitHub UI

1. **Ruleset on `aaas-app-demo` `master`**: require a PR and the `test` and `build` checks, block
   force-push. It has none today; a PR was merged mid-CI on 26 September (finding 21).
2. **A separate agent token**: fine-grained, `aaas-deployments` + `aaas-app-demo` only, Contents +
   Pull requests read/write, Actions read, **no Workflows**. Keep `.github_PAT_dont_delete` as the
   operator token. The agent then can no longer write to `aaas-agent`.

## State to verify before starting

- **Is infrastructure running?** It was left **up** on 26 September (PR #15, image `ac24bca`),
  unless Martin destroyed it. If running: `/ready` → `migration: 20260926142702_MarkItemDone`.
  If destroyed: latest `destroy` run green *and* the URL no longer resolves.
- `aaas-agent` at `23f6fd4` or later, 83 tests passing; `aaas-deployments` at `6764cbe` or later.
- No `index.lock` / `HEAD.lock` left in any repo's `.git` (finding 21).
- Did the ruleset and the second token happen? If not, say so before starting; do not reuse the
  operator token for the agent silently.

## The work

1. **Put the loop in the harness, not in the agent's turn.** After the PR, the harness waits for
   the checks (`gh pr checks`), and on failure starts the next exchange with the failed job's log
   (`gh run view --log-failed`, trimmed). Up to two rounds, then stop with the agent's explanation.
2. **Get a real failure.** See decisions. Whatever produces it must be a failure CI catches that
   the agent could plausibly have missed, not a contrived one.
3. **Run it once**, timed per round: prompt → PR → red → fix → green → merge → deployed.
4. **Record** rounds, cost per round and in total, and whether the fix was right, in `FINDINGS.md`.

## Decisions I will need from you

- **Fresh session per round, or continue the conversation.** Expect me to argue *fresh*: finding
  14 says a follow-up pays for the whole context again. A new session gets the brief, the PR diff
  and the failure log, nothing else. Measure both if cheap.
- **How to produce the failure.** Options: (a) a harness flag that drops runbook step 4 (local
  build/test/format) so the agent's first push meets CI unprepared - realistic, deterministic;
  (b) a brief that pulls toward a CI-only check (`check-migrations.sh`, the real-Postgres
  migration apply); (c) wait for a natural failure across several briefs. Expect me to propose (a)
  first and (b) if time allows.
- **Who merges on green.** Me, as in 4c, or automatic. Expect me to argue: still me - automatic
  merge is OQ-5 and wants its own session.

## Done when

- The harness can wait for checks and feed a failure back, with tests for the parts that decide.
- One run goes red, is fixed by the agent within two rounds, and reaches a green readiness gate.
- Rounds, time and cost per round are in `FINDINGS.md`.

## Explicitly not this session

- Fix-forward after a *deploy* failure (readiness gate red). That is OQ-21 territory - revert is
  the recovery and it works.
- A new app from nothing (repo provisioning, OQ-15). Automatic merge (OQ-5). Anything commercial (D-19).

## Carried over

- **Tests that prove nothing** (finding 21): the run-2 test re-implements the query it claims to
  check. An endpoint test against CI's real Postgres would catch the class. Candidate runbook or
  template change.
- **OQ-21 — "immutable once merged" is the wrong rule.** Unchanged.
- **A green destroy is not evidence the stack is gone (finding 20).** Post-destroy `az group exists`
  assertion still not built.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **`verify-isolation` cannot see token permissions.** The 403 probe from 26 September could become
  a start-up check.
- **`phase4c.sh`** (in the `aaas` folder, outside the repos) is the pattern for driving runs from a
  linked session; generalise or delete.
