# Next session

Written 14 October 2026, when AaaS was paused. Replaced wholesale at the end of every session - this file is intent,
not history. What actually happened lives in `FINDINGS.md`, where things stand lives in `STATUS.md`, and the ordered
plan is `AaaS-context.md` §9.

**AaaS is paused.** Martin is working on a new track in its own Project: an AI developer setup, built against a
learning app. This file is for the day AaaS resumes. If you are in the new Project, you are in the wrong file.

---

## Topic

**Roadmap step 5c: spec-testers test screens** (OQ-27). The *Screen* section of a request gets hidden acceptance
tests through the page, the way *Interface* gets them through the routes, so a UI change is no longer graded by its
own builder.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

- **On 11 October a screen rule went live broken** (finding 32): an empty note was shown blank instead of "—".
  - Both spec-testers did as their runbook says: they marked the screen rules "not tested", correctly.
  - The harness merged anyway, because a page under `src/` is inside the D-26 class.
  - So for UI changes, D-26 is weaker than it looks.
- **The fix is cheap, because of D-30.** A static SSR page is HTTP in and HTML out, as testable as a route.
  `PageTest` already renders pages and submits their forms the way a browser does.
- **The workbench (D-31) moved spec-testers to ship time.** This step is what makes their tests cover what the
  operator previewed.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` 32, then 33 | The defect, the screen design; the workbench and why AaaS paused |
| `AaaS-context.md` D-26, D-30, D-31, OQ-27, OQ-28 | The merge class, the screen rules, ship-time spec-testers |
| `aaas-deployments/agent/write-acceptance.md` | The spec-tester runbook that gets a "test the Screen section" part |
| `aaas-app-template/tests/App.Tests/Acceptance/AcceptanceBase.cs`, `tests/App.Tests/Pages/PageTest.cs` | What the spec-tester can call today, and what pages already have |

## State to verify before starting

- **Infrastructure: destroyed** since 11 October (`destroy` run 38151400239). Leave it down unless the last step
  deploys.
- Heads as of 14 October:
  - `aaas-agent` `master` has #13 (`workbench.sh` without `origin/HEAD`);
  - `aaas-app-demo` has #31; `aaas-app-template` has #12;
  - `aaas-deployments` is `3881b3f`, still pinning image `d3ab5df` and `app-stack` v0.4.0;
  - `aaas-docs` has this file.
- `aaas-app-demo` on Martin's Mac has an untracked `requests/item-soon-due-items-marked.md`, deliberately ambiguous
  (finding 33). It is his.
- No open deployment PRs. #42 and #43 were closed on purpose.
- GitHub is linked in claude.ai, or the operator PAT is used through the Mac's shell (finding 33, "What cost time").

## The work

1. **`AcceptanceBase` gains `GetPageAsync(path)` and `SubmitFormAsync(form, fields)`.** HTML in the response, forms
   posted as a browser would, using AngleSharp as `PageTest` does. In the template first, then the demo.
2. **The runbook** (`write-acceptance.md`) gets a section on testing the *Screen* section through the page:
   - what is shown;
   - what each control does;
   - what a refusal looks like.

   Black-box: no component names, only what a person sees.
3. **Score it as finding 27 did.**
   - Planted screen defects: the 11 October empty note; a missing link; a refusal not shown; a wrong order.
   - Then two spec-testers on the `item-table` request of 11 October.
4. **Decide the merge class** (D-26) for a change that touches pages.

## Decisions I will need from you

- **Does a page change stay inside D-26 once screens are tested?** I expect to argue yes, if two spec-testers'
  page tests pass.
- **Is the empty-note case a regression test** in the template, or only in the demo's record?

## Done when

- A planted screen defect, the 11 October empty note among them, goes red on a spec-tester's test.
- A screen-only change is merged on independent tests.

## Explicitly not this session

- Step 6 (a new app from nothing).
- The operator's chat on his local branch (OQ-28).
- Hosting the chat (OQ-4).
- Anything commercial (D-19).

## Carried over

- **OQ-28, helping the operator write a request.** The workbench works, but its first real use stopped at writing
  the request (finding 33).
- **The harness only sees GitHub;** `run.sh --task build` does not push the request. `workbench.sh` does.
- **The harness's merges look like Martin's** on GitHub.
- **The spec-testers run one after the other.** Parallel would save about 4 minutes, and matters more now that ship
  is the only place they run.
- **The reporter has only been seen on the success path** (OQ-22). **The chat is one conversation in memory** (OQ-4).
- **A green destroy does not free the name** (finding 30). **Region capacity** (OQ-25), with the demo in
  `northeurope`.
- **The shared spec-tester blind spot** (finding 29). **Pending requests in the demo:** `item-tags` (predates the
  template) and `item-upcoming`. Old PRs #14-16 and #6 can be closed.
- **Smaller known issues:**
  - policy false positives;
  - the permanent `workload_profile_name` diff;
  - the operator token is admin;
  - `gh pr close` is refused by policy;
  - no `.terraform.lock.hcl`;
  - no ruleset on `aaas-deployments`;
  - a capped run loses its work (finding 22).
