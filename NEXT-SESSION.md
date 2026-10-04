# Next session

Written 4 October 2026 at the end of the Phase 7 session. Replaced wholesale at the end of every
session — this file is intent, not history. What actually happened lives in `FINDINGS.md`, where
things stand lives in `STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 2a: a spec-tester role.** Can a separate agent session, given only a brief and its
Interface section, write acceptance tests as good as the operator's hidden ones? Measure it on the four
briefs we already have, against PRs #13–16 and a few planted bugs. No new app runs and no Azure.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Finding 26: the agent's own green was wrong on 1 of 3 briefs, the largest. Only tests written from the
spec by someone who never saw the code caught it. Auto-merge (OQ-5) needs that as a required check, and
the operator cannot write those tests per change. So the question is whether a model can, without seeing
the PR (OQ-23). If it can't, auto-merge is further off than the build so far suggests, and we should know
before building the gate in step 2b.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` finding 26 | The table, the #16 miss, and why the failure mode is now missing coverage rather than wrong tests |
| `aaas-agent/acceptance/` | The operator's hidden tests and `AcceptanceBase.cs`: the baseline to beat, and the fixture the spec-tester writes against |
| `aaas-agent/briefs/item-upcoming.md`, `item-tags.md`, `projects.md` | Briefs with an Interface section; `item-priority.md` has none |
| `aaas-agent/README.md` + `aaas-deployments/agent/create-app.md` | How a task and its runbook are wired, for a new `write-acceptance` task |

## Before the session - Martin

1. **Don't merge #14, #15 or #16 in `aaas-app-demo` yet.** They are this session's test corpus. Merging
   one changes `master`, and with it what the next spec-tester sees.
2. Decide whether to commit `aaas-agent/acceptance/` and the three new briefs (see Carried over).

## State to verify before starting

- `aaas-app-demo` PRs #14, #15, #16 are open at `07b72ce`, `0f09129`, `fc6efd0`. `master` is still `0ac9d66`.
- `rg-demo-dev` does not exist, and no `apply` has run since `destroy` 36908084546.
- `aaas-agent/acceptance/` has 4 `*.acceptance.cs` files plus `AcceptanceBase.cs`, `run-acceptance.sh`, `README.md`.
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` / `maintenance.lock` in any repo's `.git`.
- `gh` works in the linked shell (reinstall into `$HOME/bin`, linux arm64; STATUS.md).

## The work

1. **A `write-acceptance` task in the harness.** Input: a brief. It sees `AcceptanceBase.cs`,
   `EndpointTest.cs`, and a checkout of `aaas-app-demo` at the brief's base commit, never the PR. Output: one
   `<brief>.acceptance.cs` in the run directory. No push, no PR, and no `gh`.
2. **Run it on the four briefs**, once each, and twice on `projects`.
3. **Score its tests** in the container, on PRs #13–16 and on the planted bugs below. Compare them with the
   operator's tests on the same PRs:
   - #16 must go red, on the trimmed-name boundary or another real defect.
   - #13, #14 and #15 must stay green. Any red is either a wrong test or a real bug the operator's tests
     missed. Adjudicate against the brief text.
   - Planted bugs, at least one per brief: off-by-one window, case-sensitive tag filter, percent rounded
     up, done items in the priority list.
4. **One finding**: for each brief, the spec-tester's catches, its false reds, its cost, and its test
   count against the operator's. Then a one-line position on step 2b.

## Decisions I will need from you

- **What the spec-tester sees.** Expect me to argue: the brief, the fixture, and `master` at the base
  commit, so it can compile and see existing routes and shapes. Never the PR. Seeing `master` is what a
  real spec-tester would have. Withholding it means its tests fail to compile, which measures the wrong thing.
- **Harness task or a plain session.** Expect me to argue: a harness task. It is the product role, and
  `report.md` gives cost and policy refusals for free. A plain session would be quicker today and teach
  nothing about the role.
- **What counts as a false red.** Expect me to argue: a red that the brief text does not support. The
  operator's tests were written to the same rule.
- **What happens to #14–16 afterwards.** Expect me to argue: merge #14 and #15 at the end of the session,
  then send #16's hidden-test result to a fix round. That makes this the first fix round fed by a test the
  agent didn't write, and a one-run preview of step 2b. Or close #16 if you would rather keep step 2b clean.

## Done when

- The spec-tester has run on four briefs, plus a second `projects` run, and each is scored against #13–16
  and the planted bugs, beside the operator's tests.
- One finding, and a one-line position on step 2b in `AaaS-context.md` (OQ-23).

## Explicitly not this session

- The required-check plumbing: where hidden tests live in CI, and auto-merge itself. That's step 2b.
- The fix-round assertion guard. It is unsized: 0 edits in this session's one round.
- A new app from nothing (OQ-15), OQ-21, the `workload_profile_name` diff, and anything commercial (D-19).
- An `apply`. Nothing this session needs Azure.

## Carried over

- **Untracked in `aaas-agent`:** `acceptance/` (new, 7 files); briefs `item-upcoming.md`, `item-tags.md`,
  `projects.md` (new), plus last session's `item-due.md`, `item-priority.md`, `notebook-fix-cli-missing.md`
  and the `.gitignore` line. The acceptance files are only hidden while no run can read them: a run mounts
  `runs/<id>` alone (finding 23), so committing them to `aaas-agent` is safe for the harness. They would
  still be public on GitHub.
- **Run drivers:** `phase7.command` is current (watcher only). `session-1001.command`, `phase6.command`,
  `notebook-agent.command` and `notebook-app.command` can be deleted, along with their log folders.
- **Policy false positives:** pipes after `dotnet` (`| tail`, `| grep`), twice in finding 26. Also last
  session's `nuget` path and heredoc `fix:` cases. Same shape each time: the policy reads arguments as commands.
- **The permanent in-place diff** on the Container App after every create (`workload_profile_name`). Harmless so far.
- **The operator token is admin**, so the `aaas-app-demo` ruleset does not bind it.
- **OQ-21: "immutable once merged" is the wrong rule.** Roadmap step 4.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **`aaas-deployments` has no ruleset.** A deployment PR can be merged before `gate`.
- **A capped run still loses its work** (finding 22).
- **Merged branches to delete:** `test/endpoint-tests` on the template and demo; `feat/priority-list` on the demo.
