# Next session

Written 11 October 2026 at the end of roadmap step 5. Replaced wholesale at the end of every session —
this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things stand
lives in `STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 5b: a screen for the user (OQ-26)**, if Martin agrees at the start. The alternative is step 6, a new
app from nothing (OQ-15). This session starts by choosing between them.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

On 11 October the §2 user asked for something in the chat. It was built, tested, merged and deployed with no
human merge (finding 31). Then they were told to "open an item", in an app that has no screens. The golden path
deploys a backend only, so §1's "web frontend" is missing. Until it exists, every change the conversation
ships is one its user cannot use. A new app from nothing (step 6) would be just as unusable, which is why 5b
should come first.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` 31 | The conversation run, and where the missing UI showed up |
| `AaaS-context.md` §1, §2, §4.2, §4.3, OQ-26, D-20 | What the archetype promised, and the scaffold a UI must fit into |
| `aaas-app-template/AGENT.md` | Conventions and CI checks a UI convention joins |
| `aaas-deployments/agent/create-app.md`, `agent/intake.md` | What the builder and intake are told; both change if the user's change becomes a screen |

## State to verify before starting

- **Infrastructure:** the demo is **running in `northeurope`**, kept up by decision. `/ready` and
  `deployments/dev/demo/applied.json` should both say image `1b50207`, migration `20261010133821_AddProjects`.
- `aaas-agent` `master` is `dbb20b3` (converse, #8 and #9). Nothing is called `aaas-converse` in `docker ps`.
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` in any repo's `.git`; your local repos are pulled.

## The work (sketch, settled once 5b is chosen)

1. **A UI in the template and the demo.** Server-rendered pages in the same image, with no JavaScript toolchain:
   the item list, one form, and the existing routes behind them. Add a convention in `AGENT.md` with a CI check:
   every page renders `200` against Postgres, and every form posts to a route that exists.
2. **The builder writes the screen with the route.** `create-app.md`: a change the user will use gets its page
   in the same PR. The spec-testers stay on the routes.
3. **The answer says where.** The facts gain "where in the app" (the page path the builder reports, checked
   to exist on the live app). The reporter may describe only that.
4. **One conversation end to end**, with Martin as the user. The change shows up as something they can click.

## Decisions I will need from you

- **5b or 6.** I expect to argue for **5b**, for the reason above. Step 6 is provisioning work that has no
  user value while apps have no screens.
- **How the pages are built.** I expect to argue for **Razor (static server-side rendering) in the existing `App`
  project**:
  - one image, one language (D-20), the compiler as a gate (D-17), and nothing for an SMB's Microsoft partner
    to learn
  - not a SPA: that would add a Node build, a second test toolchain and a second deployable for no gain at
    this size
- **What the spec-testers test.** I expect to argue: **routes only**, as now. A page gets the deterministic
  render-and-form check, not acceptance tests. Browser tests in CI (Playwright) cost minutes per change and are
  where flakiness lives. Revisit if a real defect hides in a page.
- **Infrastructure:** keep the demo up (about $17-20 a month), as decided on 11 October.

## Done when

- A change asked for in plain words in the chat shows up, live, as something the user can click, and the
  answer they get names where it is, truthfully.

## Explicitly not this session

- Step 6 (a new app from nothing), unless chosen instead. The control plane (OQ-8). Anything commercial (D-19).

## Carried over

- **The harness's merges look like Martin's** on GitHub, now on deployment PRs too (#25, #36). The harness
  should comment on each PR it merges.
- **The spec-testers run one after the other**: 13 of the 26 minutes on 11 October. Running them in parallel
  saves about 6 minutes; they are already isolated from each other.
- **The reporter has only been seen on the success path.** A red deploy reported through the chat is
  untested (OQ-22).
- **The chat is one conversation in one container's memory.** A restart loses it (OQ-4).
- **A green destroy does not free the name** (finding 30). `destroy.yml` should wait until the deletion has
  *finished*, and `apply.yml` should refuse to create into a resource group that is still being deleted.
  Both are workflow changes, so Martin pushes them.
- **Region capacity** (OQ-25): the demo's `location` is `northeurope` until it is moved back deliberately.
- **The shared spec-tester blind spot** (finding 29): a "left out" rule tested at one value only.
- **Pending requests in the demo:** `item-tags`, `item-upcoming`. Old PRs #14-16 can be closed.
- **Policy false positives**, the permanent `workload_profile_name` diff, the operator token is admin,
  `gh pr close` refused by policy, no `.terraform.lock.hcl`, no ruleset on `aaas-deployments`, a capped run loses
  its work (finding 22).
