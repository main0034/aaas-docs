# Next session

Written 12 October 2026, from the prioritisation at the end of roadmap step 5b. Replaced wholesale at the end of every
session - this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things stand lives in
`STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 5a: a local workbench.** Martin asks for a change, previews it on his Mac, edits the request until it
is right, and only then ships it. Chosen by Martin on 11 October, ahead of 5c (spec-testers test screens) and 6 (a new
app from nothing).

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Martin wants to use the harness himself, and three things are missing:
- **Seeing the change before it ships.** Today the only way to see a change is to merge and deploy it, which takes
  about 20 minutes on Azure. A local preview is seconds, and costs nothing while the demo is destroyed.
- **Iterating on the requirement.** Today the request is frozen at "Go ahead". Martin wants to read it, change it, and
  see the change follow.
- **Rider.** The request is a file in the app repo, so Rider can edit it; running and previewing should be a click there.

It is also the interim answer to OQ-27. On 11 October a screen rule went live broken, because nobody independent
tested the screen (finding 32). With a preview before anything merges, Martin is that check, until 5c automates it.
And it settles *when* the spec-testers run, which 5c builds on.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` 32, then 28-29 | Screens and the defect; the change record and the spec-testers' isolation |
| `AaaS-context.md` D-25, D-26, D-29, D-30, OQ-27 | What the order "tests before code" was for, and what replaces it |
| `aaas-agent/harness/main.py` (`run_change`), `harness/change.py`, `run.sh` | The steps the workbench splits apart |
| `aaas-app-demo/README.md` "Local development", `AGENT.md` | How the app already runs against a local Postgres (`PGPASSWORD`) |

## State to verify before starting

- **Infrastructure: destroyed** (`destroy` run 38151400239, 11 October 15:28 UTC; the hostname does not resolve). Leave
  it down: this session needs no Azure until the last step, and only if that step deploys.
- `aaas-agent` `master` is `4370edd`; `aaas-app-demo` `d3ab5df`; `aaas-app-template` `544cf02`; `aaas-docs` has #19 and
  this file. Your local repos are pulled. `aaas-app-demo` has `backup/master-1615` and an untracked `git_magic.sh`.
- Docker runs on the Mac (the harness needs it already). `postgres:16` pulls.
- **GitHub is linked in claude.ai** for this session, or the bundle route of 11 October is planned for
  (`aaas/.session-bundles/`, deleted afterwards).

## The work

The loop, with the commands it becomes:

```
request ──► build ──► preview ──► (edit request ──► rebuild ──► preview)* ──► ship
```

1. **Build without shipping.** `run.sh --task build --app-repo aaas-app-demo --change <name>`:
   - the builder works on `change/<date>-<name>` from `requests/<name>.md` and pushes the branch
   - it opens **no PR**, and no spec-tester runs
   - the change can come from intake in the chat ("Try it" next to "Go ahead") or from a file Martin wrote
2. **Preview.** In the app repo, `docker compose -f preview.yml up` builds the branch's image and starts it next to
   `postgres:16` on `localhost:8080`, with `migrate` run first, exactly as the init container does.
   - `scripts/preview-seed.sh` adds a few rows through the routes, never SQL.
   - The image is the real one (D-20). If the preview works, the image works.
3. **Iterate.** Martin edits `requests/<name>.md` **on the change branch**, in Rider, and commits.
   `run.sh --task rebuild --change <name>`:
   - a fresh builder session gets the new request, the old one, and the branch's diff, and changes the code to match
   - the request's git history is the record of how the requirement moved
4. **Ship.** `run.sh --task ship --change <name>` runs today's `create-change` from the branch onwards:
   - the request moves into `changes/<id>/brief.md`
   - two spec-testers write hidden tests from the *final* request, isolated on `master` as today
   - PR, CI, fix rounds, D-26 merge, and delivery if Azure is up
5. **Rider.** `.run/*.run.xml` in the app repo, shared: *Preview*, *Rebuild*, *Ship*, and *App (local Postgres)*, the
   last for debugging with breakpoints. No plugin. Template first, then the demo.
6. **One real loop** with Martin: ask, preview, change the request once, rebuild, preview, ship. Time each step.

## Decisions I will need from you

- **Spec-testers at ship time only.** I expect to argue yes. Running them per iteration costs about $1.30 and 10 minutes,
  and throws their tests away whenever the request changes.
  - This amends D-25. Its "tests written before any code existed" becomes "tests written by someone never shown the
    code". The spec-tester gets `master` only, so it is still blind to the branch.
  - Independence never came from the order. It came from the isolation, and the tests stay off the builder's disk.
- **The request lives in `requests/<name>.md` on the change branch while iterating**, editable by you. It moves into the
  immutable `changes/<id>/` only at ship. I expect to argue for this over editing in the chat page. Rider and git
  already do editing and history well.
- **Preview runs the real image with Docker Compose** rather than `dotnet run`. I expect to argue for the image, because
  the image is what deploys. `dotnet run` stays as the Rider debug configuration.
- **The chat page gets "Try it" only if it is cheap.** I expect to argue that the workbench is for you as operator first,
  and that the §2 user's preview (OQ-26's "conversation plus a preview", §2) is a later hosted step (OQ-4).

## Done when

- Martin asks for a change, clicks through it on `localhost`, changes the request once and sees the rebuild follow, then
  ships it, and the spec-testers' tests from the final request run in CI. All of it can be started from Rider.

## Explicitly not this session

- 5c (spec-testers test screens) and step 6 (a new app from nothing), which come next, in that order. A Rider plugin. A
  hosted preview for the §2 user (OQ-4). The control plane. Anything commercial (D-19).

## Carried over

- **OQ-27, who tests a screen independently**: step 5c. Until then, the preview is the screen check.
- **A probe item, "Empty note probe"**, was in the destroyed demo. It is gone with it.
- **Antiforgery is off** for the screens (no sign-in). It changes when end users sign in.
- **The harness's merges look like Martin's** on GitHub. Claude's manual merges of deployment PRs #38, #39 and #41
  carry a comment; the harness's do not yet.
- **The spec-testers run one after the other** (9 of 20 minutes on 11 October). Parallel would save about 4 minutes, and
  matters more once ship is the only place they run.
- **The reporter has only been seen on the success path** (OQ-22). **The chat is one conversation in memory** (OQ-4).
- **A green destroy does not free the name** (finding 30); **region capacity** (OQ-25), with the demo in `northeurope`.
- **The shared spec-tester blind spot** (finding 29). **Pending requests in the demo:** `item-tags`, `item-upcoming`.
  Old PRs #14-16 and #6 can be closed.
- **Policy false positives**, the permanent `workload_profile_name` diff, the operator token is admin,
  `gh pr close` refused by policy, no `.terraform.lock.hcl`, no ruleset on `aaas-deployments`, a capped run loses
  its work (finding 22).
