# Next session

Written 10 October 2026 at the end of roadmap step 2b. Replaced wholesale at the end of every session —
this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things stand
lives in `STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 2c: two spec-testers per change, their tests combined, then auto-merge for route-only
changes.** `create-change` runs two independent spec-tester sessions on the same brief and commits both
files into `changes/<id>/`. Measure the combined gate on planted bugs against single runs. If it holds,
turn on auto-merge for one class of change, with the harness doing the merge.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Step 2b built the gate and ended without an auto-merge, for a recorded reason (finding 28). One spec-tester
run went green on #16's known defect that two earlier runs caught. Across seven runs, the misses differ
between runs of the same brief: trimming in one, done items in another. So the union of two independent
runs is the cheapest lever left, at about $0.7 a run, and it is the last thing between OQ-5 and a first
merge without a human. Step 3 (a new app from nothing) is the alternative if you would rather move on.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` findings 27-28 | The seven spec-tester runs, what each missed, the two harness bugs |
| `aaas-agent/harness/main.py` `run_change`, `harness/change.py` | Where a second spec-tester slots in |
| `aaas-app-demo/scripts/check-changes.sh`, AGENT.md "The change record" | What CI enforces |
| `aaas-agent/acceptance/score/` (untracked) | The scorer and planted bugs, to extend |

## State to verify before starting

- `aaas-agent` `master` is `6b1ce8b` or later (#5). `aaas-app-demo` `master` is `97b30b4` (#17) and has
  `changes/2026-10-01-item-priority/`; `aaas-app-template` `master` is `4e6d02b` (#4).
- `aaas-app-demo` #18 and #19 are open and green; `aaas-deployments` #26 is still unmerged. `rg-demo-dev` does not exist.
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` in any repo's `.git`. Martin's `aaas-agent` is fast-forwarded
  (the image is built from the local tree).

## The work

1. **`--spec-testers 2`**: two spec-tester sessions, sequential, each in its own isolated checkout, each with its
   own namespace (`...ItemSummaryA`, `...B`). Both files and both `NOTES.md` go into `changes/<id>/`. The builder
   still sees neither.
2. **Measure on what we have.** Run two fresh spec-testers on each of `item-summary`, `projects`, `item-tags` and
   `item-upcoming` without a builder (`write-acceptance`, twice). Score each run alone and each pair combined on the
   planted bugs (`score.py`, extended with finding 28's 9 for `item-summary`) and on #16's head. That gives 8 new
   runs and 4 pairs: is the union's miss rate materially below a single run's?
3. **If it holds: auto-merge.** The harness merges a green PR when all of these hold:
   - the PR adds no migration (the diff has nothing under `src/App/Migrations/`)
   - the PR touches nothing outside `src/`, `tests/` and its own `changes/<id>/`
   - the acceptance files are byte-identical to the harness's commit
   - every earlier change's tests pass, which CI already enforces

   The merge is deterministic code in the harness (D-17), never a model's decision. Prove it once with a new brief.
4. One finding, and the OQ-5 position updated.

## Decisions I will need from you

- **Who merges.** I expect to argue for the harness, with the operator token, after the checks above. That needs no
  new credential (D-14), and the ruleset still requires `test` + `build`. The alternative, GitHub's auto-merge
  setting, would let any green PR merge, including one from a human that skipped the change record.
- **The class.** I expect to argue for route-only changes with no migration and no new package. A migration stays
  gated (OQ-7); so does a new dependency, because nothing yet reviews one.
- **What counts as holding.** I expect to argue: the union catches every real defect so far and at least 90% of
  planted bugs, with 0 false reds. If it does not, record it and move to step 3 rather than add a third run.

## Done when

- Two spec-testers per change in `create-change`, and a measured single-versus-pair miss rate.
- Either one change merged by the harness without a human, or a recorded reason why not, with step 3 next.

## Explicitly not this session

- Deployment (`apply`), and merging #26. A new app from nothing (OQ-15). Superseding accepted behaviour (OQ-24).
- OQ-21, the `workload_profile_name` diff, anything commercial (D-19).

## Carried over

- **Open PRs to decide:** `aaas-app-demo` #18 (`/items/summary`) and #19 (projects, supersedes #16), both green with
  change records; #14, #15 and #16 predate the change record; `aaas-deployments` #26 (deploy of #17, no runtime
  change). Merging any app PR opens a deployment PR whose apply is a create.
- **Briefs in `aaas-agent/briefs/` are an inbox now.** The app repo holds the record. `item-due` (#11) has no record:
  backfill it if someone writes its acceptance tests. The notebook briefs belong to the notebook app.
- **Untracked in `aaas-agent`:** `acceptance/` (operator tests, `spec-tester/`, `score/`), the briefs, and the
  `.gitignore` line. Commit the scorer if step 2c extends it.
- **Run drivers:** `session-1010.command` is current. `phase7.command`, `phase6.command`, `session-1001.command`,
  `notebook-*.command` and their log folders can go.
- **Policy false positives:** pipes after `dotnet` (finding 26), the `nuget` path, heredoc `fix:`, a `grep` pattern with
  `\|` (finding 28).
- **The permanent `workload_profile_name` diff**; **the operator token is admin**; **OQ-21**; **`gh pr close` refused by
  policy**; **no `.terraform.lock.hcl`**; **no ruleset on `aaas-deployments`**; **a capped run loses its work** (finding 22).
- **Merged branches to delete:** `test/endpoint-tests` (template, demo), `feat/priority-list` (demo).
