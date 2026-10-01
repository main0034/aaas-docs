# Next session

Written 1 October 2026 at the end of the endpoint-tests session. Replaced wholesale at the end of
every session — this file is intent, not history. What actually happened lives in `FINDINGS.md`,
where things stand lives in `STATUS.md`.

---

## Topic

**Phase 7, measured: is green correct?** Write acceptance tests the agent never sees, from the brief
alone, *before* each run. Run the agent on three briefs of increasing size. After its PR is green,
run the hidden tests against the PR branch and score each brief: green-and-correct,
green-but-wrong, or red at the end.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Finding 25 made the agent's tests real: it writes exact-row endpoint tests against Postgres without
being told to. But its tests are still graded by the code they test. In that run a fix round edited
an assertion until CI agreed with the code. It was right that time, and the same move turns a caught
bug green. OQ-5 (auto-merge) and Phase 7 (open-ended generation) both need one number: **how often
green is wrong**. Hidden acceptance tests measure that deterministically (D-17), with no reviewing model.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` finding 25 | The fixture, the planted bugs, the agent's run and its assertion-editing fix round. |
| `aaas-app-template/AGENT.md`, "Every new or changed route gets an endpoint test" | What the agent is told. Acceptance tests use the same base class. |
| `aaas-app-demo/tests/App.Tests/Postgres/` | `EndpointTest`, `SeedAsync`, `TEST_POSTGRES`. Acceptance tests derive from it. |
| `aaas-agent/README.md`, fix-forward section | Where a fix round's diff can be read, and where a guard would go. |

## Before the session - Martin

1. **Decide on `aaas-app-demo` PR #13** (the agent's priority list, green after one fix round). Merge
   it — and its deployment PR — or close it. Either way, say which. It is the calibration brief below.
2. **Infrastructure:** destroy it if the gap will be more than a few days (~$17–20/month). If it is
   destroyed, start an `apply` from the UI at the start of the session. Runs can do without a live
   stack until the last step, but the deploy check needs one.

## State to verify before starting

- Last `apply`/`destroy` on `aaas-deployments`, and whether `rg-demo-dev` exists. It matches what Martin said.
- `aaas-app-demo` `master` is `55b18eb` or later (endpoint tests), or later still if #13 was merged.
  The image pinned in tfvars matches.
- `aaas-app-template` `master` has `tests/App.Tests/Postgres/` and the `endpoint tests` CI step.
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` / `maintenance.lock` in any repo's `.git`.
- `gh` works in the linked shell (reinstall into `$HOME/bin` if the session home is new — STATUS.md).

## The work

1. **Calibration:** write acceptance tests for `briefs/item-priority.md` from the brief only, and run
   them against PR #13's branch. Expect green; if not, the brief or the tests are the problem.
2. **Three briefs, written by me, reviewed by Martin:** small (one route, one filter), medium (a
   migration plus a relation — e.g. tags on items, filter by tag), and larger (a second entity with an
   aggregate — e.g. projects with items and a progress summary).
   Each brief has an acceptance file in `aaas-agent/briefs/<name>.acceptance.cs`, written before the run.
3. **Run each** with `--fix-rounds 2`. After green: copy the acceptance file into a checkout of the
   PR branch in the cloud container (Postgres 16 + .NET 10 there, ~5 minutes to set up) and run it with
   `TEST_POSTGRES`. Record the score, the cost, the fix rounds, and **every assertion a fix round
   changed**.
4. **One finding** with the table. Then a one-line position on OQ-5 that the numbers support.

## Decisions I will need from you

- **Briefs name the route.** Hidden tests must know what to call. Expect me to argue: each brief ends
  with an *interface* line (route, parameters, response shape) — what the §6 intake agent would put in
  a spec. Scoring the agent's naming is not the question.
- **Where acceptance tests run.** Expect me to argue: in my container, never in the app repo or its CI.
  Anything in the repo is visible to the next run, and run directories are isolated (finding 23).
- **What counts as wrong.** Expect me to argue: acceptance tests assert only what the brief states
  unambiguously; an ambiguity is logged as a brief defect, not scored against the agent.
- **A guard against weakened assertions in fix rounds — now or after the numbers.** Expect me to argue:
  after. Record each changed assertion this session. A deterministic guard (a fix round may not change
  an existing test's expected values without saying why in the PR) is cheap, but should be sized by
  how often it fires.

## Done when

- The calibration brief scores green-and-correct, or its disagreement is explained.
- Three briefs run, each scored by hidden acceptance tests, with cost, rounds and changed assertions
  recorded in one finding.
- A one-line OQ-5 position, backed by that table, is in `AaaS-context.md`.

## Explicitly not this session

- Turning on auto-merge (OQ-5). This session produces the evidence, not the switch.
- A new app from nothing (OQ-15). OQ-21. The `workload_profile_name` diff. Anything commercial (D-19).
- Running endpoint tests inside the harness (D-24 settled it).

## Carried over

- **The permanent in-place diff** on the Container App after every create (`workload_profile_name`
  "Consumption" → null, empty `args`/`command`). Harmless so far.
- **The operator token is admin**, so the `aaas-app-demo` ruleset does not bind it.
- **OQ-21 — "immutable once merged" is the wrong rule.** Unchanged.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **`verify-isolation` cannot see token permissions.** A push dry-run could become a start-up check.
- **Policy false positives:** a `find` whose path contained `nuget`; a heredoc commit message
  starting `fix:` (finding 25). Same shape: the policy reads arguments as commands.
- **A capped run still loses its work** (finding 22).
- **Untracked in `aaas-agent`:** five `briefs/*` (`item-due`, four `notebook*`) plus
  `briefs/item-priority.md` from this session, the `.gitignore` line (`.idea`). Martin's
  to commit or discard; `item-priority.md` is needed next session.
- **Run drivers:** `session-1001.command` is current (watches `session-1001-logs/RERUN`). `phase6.command`,
  `phase4c.sh`, `notebook-agent.command` and `notebook-app.command` are superseded and can be deleted.
- **Template and demo `test/endpoint-tests` branches** are merged and can be deleted.
