# Next session

Written 11 October 2026 at the end of roadmap step 4. Replaced wholesale at the end of every session —
this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things stand
lives in `STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 5: the first missing plane, one thin path through it.** Two candidates, chosen by interest
(D-19). This session starts by choosing:

- **A. Intake and conversation** (OQ-4, OQ-22): the §2 user describes what they want in a chat. An intake role
  turns that into `requests/<name>.md` from the template, asking only what the template needs. Then
  `create-change` runs, and the user is told the result in plain words, including when it failed.
- **B. Control plane** (OQ-8): one page or command that lists every estate (module, archetype, image, applied
  migration, health) from the tags and `applied.json` (D-28). Then one module upgrade rolled out through it.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

The delivery and agent planes are done for an existing app. A request becomes a merged, deployed change, and
every estate says what it runs. Two of §5's five planes are still empty, and the product cannot be used by its
§2 user without one of them. Step 6 (a new app from nothing) was moved after this on 10 October.

## Read first, in this order

| File | Why |
|---|---|
| `AaaS-context.md` §2, §5, §6, OQ-4, OQ-8, OQ-22 | The user, the planes, what each candidate has to answer |
| `aaas-app-demo/requests/TEMPLATE.md` | What intake (A) has to produce |
| `aaas-deployments/deployments/dev/demo/applied.json`, `app-stack` README "tags" | What the control plane (B) can read |
| `FINDINGS.md` 29-30 | The change loop and version visibility this builds on |

## State to verify before starting

- **Infrastructure:** the demo was **running in `northeurope`** at the end of step 4, unless Martin destroyed it
  (STATUS says which). If it is up, decide first whether this session needs it. A only needs it at the end; B
  reads it throughout.
- `aaas-deployments/deployments/dev/demo/applied.json` matches `/ready`. `app-stack` latest tag is `v0.4.0`.
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` in any repo's `.git`; your local repos are pulled.

## The work (sketch, settled once A or B is chosen)

- **A.**
  1. An `intake` task in the harness: a conversation that ends in a request file. Its runbook is the template's
     sections; it has no tools beyond writing that one file.
  2. A thin chat front-end, a local web page is enough, that runs intake, then `create-change`, and reports
     back in the user's words.
  3. One real request from a "non-technical" brief, end to end, with Martin playing the user.
- **B.**
  1. `estates` as a script or small page: every `applied.json` plus `az resource list --tag aaasModule` (read
     identity), and drift where they disagree.
  2. `app-stack` v0.4.1, a harmless change, rolled out to the demo through it.
  3. What OQ-8's "not all at once" needs, written down.

## Decisions I will need from you

- **A or B.** I expect to argue for **A**. It is where the §2 user meets the product, and the request template and
  `create-change` are already half of it. B is mostly a report while there is one estate; it earns its keep at N
  estates, which needs step 6 first.
- **For A: where intake runs.** I expect to argue: the same container harness, a new task, interactive. Hosting it
  for real (OQ-4) waits.
- **Infrastructure:** keep the demo up across sessions (about $17-20 a month, finding 25), or destroy and re-create
  when needed (about 11 minutes, if the region has capacity).

## Done when

- **A:** a request typed in plain words by someone playing the §2 user became a merged, deployed change, and the
  answer they got back named what changed, without them seeing git.
- **B:** every estate is listed with what it runs and whether that matches Azure, and one module upgrade went
  through that view.

## Explicitly not this session

- The other candidate. Step 6 (a new app from nothing). Anything commercial (D-19).

## Carried over

- **A green destroy does not free the name** (finding 30). `destroy.yml` should wait until the deletion has
  *finished*: `az group show` not found, and nothing tagged `deployment=<name>` left. `apply.yml` should refuse to
  create into a resource group that is still being deleted. Both are workflow changes, so Martin pushes them.
- **Region capacity** (OQ-25): `swedencentral` had no Burstable Postgres on 10-11 October. The demo's `location` is
  `northeurope` until it is moved back deliberately.
- **The harness merge looks like Martin's** on GitHub. The harness should comment on the PR when it merges.
- **The shared spec-tester blind spot** (finding 29): a "left out" rule tested at one value only.
- **Pending requests in the demo:** `item-tags`, `item-upcoming`. Old PRs #14-16 can be closed.
- **Policy false positives**, the permanent `workload_profile_name` diff, the operator token is admin,
  `gh pr close` refused by policy, no `.terraform.lock.hcl`, no ruleset on `aaas-deployments`, a capped run loses
  its work (finding 22).
