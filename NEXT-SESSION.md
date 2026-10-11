# Next session

Written 11 October 2026, evening, at the end of roadmap step 5b. Replaced wholesale at the end of every session -
this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things stand lives in
`STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 5c: spec-testers test screens (OQ-27).** The *Screen* section of a request gets hidden acceptance tests
written through the page, the way *Interface* gets them through the routes.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Since 11 October a change asked for in the chat shows up as a screen (finding 32, D-30). The first one went live with
a stated rule broken: an empty note showed blank instead of "—". Every rule in that request was about the screen. Both
spec-testers tested `GET /items` and marked the screen rules "not tested", which is what the runbook told them to do.
The builder's page test was the only check, so the builder graded itself, and the harness auto-merged it under D-26.

That undoes the point of D-25 and D-26 for exactly the changes a §2 user asks for most: what they see. Step 6 (a new
app from nothing) would multiply apps whose screens nobody independently tests.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` 32 | The screens, the run, and the defect |
| `FINDINGS.md` 27-29 | How the spec-testers were built, isolated and paired, and their measured catch rate |
| `AaaS-context.md` D-25, D-26, D-30, OQ-27 | What the change record and the merge class promise, and the screens decision |
| `aaas-deployments/agent/write-acceptance.md` | The spec-tester runbook; its *Screen* line changes |
| `aaas-app-demo/tests/App.Tests/Acceptance/AcceptanceBase.cs`, `tests/App.Tests/Postgres/PageTest.cs` | The spec-testers' base class, and the page-test helpers it should gain |
| `aaas-app-demo/changes/2026-10-11-items-list-table/` | Both spec-testers' NOTES: what they left untested and why |

## State to verify before starting

- **Infrastructure:** the demo is **running in `northeurope`**, kept up by decision. `/ready` and
  `deployments/dev/demo/applied.json` should name the image of demo #29 (the empty-note fix) if it was merged and
  deployed, otherwise `14fbc04` (#28). Migration `20261010133821_AddProjects`.
- `aaas-agent` `master` is `4370edd` (#10). `aaas-app-template` has #8-#10, `aaas-app-demo` #26-#29,
  `aaas-deployments` #37 (runbooks).
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` in any repo's `.git`; your local repos are pulled;
  `aaas-app-demo` has a `backup/master-1615` branch you can delete.

## The work

1. **Spec-testers get pages.** `AcceptanceBase` gains `GetPageAsync(path)` and `SubmitFormAsync(path, form, fields)`.
   These are black-box: HTML in the response, forms posted as a browser does. They have no component types, because
   the spec-tester never sees the code.
2. **The runbook.** `write-acceptance.md`: test the *Screen* section through the page as well as *Interface*
   through the routes. A screen rule ("no note shows —") becomes a test on the rendered HTML. Name the
   page by its path from the request, and match text, not markup.
3. **Intake writes paths.** A *Screen* section names the page path (`/`, `/app/items/{id}`) and the visible words the
   spec-testers can look for. Intake can read the pages, so it knows them.
4. **Measure it like finding 27.** Replay `items-list-table` with two spec-testers against #28's code: the empty-note
   defect must go red. Plant 4-6 screen bugs (a missing column, a wrong truncation, a dash missing, a link to the
   wrong page) and count. No false reds on #29.
5. **One chat run** with a screen-only change, merged on the spec-testers' page tests.

## Decisions I will need from you

- **5c or 6.** I expect to argue for **5c**, for the reason above.
- **Page tests as text, not markup.** I expect to argue that spec-testers assert on visible text and links in the HTML,
  never on CSS classes or element structure. Otherwise every restyle breaks accepted behaviour (OQ-24 for screens).
- **Until 5c is done: should a change to a page leave the D-26 merge class?** I expect to argue **no, keep the chat
  usable**. The demo is dev-only and the defect class is cosmetic. Make it a blocker if 5c slips past one session.
- **Infrastructure:** keep the demo up (about $17-20 a month).

## Done when

- A request whose rules are about the screen gets hidden tests that render the page, and those tests catch the
  11 October empty-note defect and most planted screen bugs, with no false reds.

## Explicitly not this session

- Step 6 (a new app from nothing). Browser tests (Playwright): static SSR makes them unnecessary. Styling or a design
  system for the screens. The control plane. Anything commercial (D-19).

## Carried over

- **The `check-pages.py` CI step** is on master in both app repos (template #9, demo #27). Martin pushed it, because
  the PAT has no Workflows scope.
- **This session had no GitHub link.** The container's proxy blocks `api.github.com` without one. Git and the API ran
  in the Mac's VM shell with the operator PAT, using bundles in `aaas/.session-bundles/` (delete the folder). Link
  GitHub in claude.ai before the next session, or plan for the same route.
- **A probe item, "Empty note probe" (id 2)**, is in the live demo with a stored `""` note. There is no delete route.
- **Antiforgery is off** for the screens (no sign-in). It changes when end users sign in.
- **The harness's merges look like Martin's** on GitHub. Claude's manual merges of deployment PRs #38-#39 carry a
  comment; the harness's do not yet.
- **The spec-testers run one after the other**: 9 of 20 minutes on 11 October evening.
- **The reporter has only been seen on the success path** (OQ-22). **The chat is one conversation in memory** (OQ-4).
- **A green destroy does not free the name** (finding 30); **region capacity** (OQ-25), with the demo in `northeurope`.
- **The shared spec-tester blind spot** (finding 29). **Pending requests in the demo:** `item-tags`, `item-upcoming`.
  Old PRs #14-16 and #6 can be closed.
- **Policy false positives**, the permanent `workload_profile_name` diff, the operator token is admin,
  `gh pr close` refused by policy, no `.terraform.lock.hcl`, no ruleset on `aaas-deployments`, a capped run loses
  its work (finding 22).
