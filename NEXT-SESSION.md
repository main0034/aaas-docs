# Next session

Written 10 October 2026, evening, at the end of roadmap step 2c. Replaced wholesale at the end of every
session — this file is intent, not history. What actually happened lives in `FINDINGS.md`, where things
stand lives in `STATUS.md`, and the ordered plan is `AaaS-context.md` §9.

---

## Topic

**Roadmap step 3: a new app from nothing (OQ-15).** One command or workflow takes a name, `aaas-app-<name>`,
to a repository that the change flow can work in and that deploys. It then *asserts* each piece of identity
before the first deploy, rather than finding out eight minutes into one. Done with a real second app: a
first request from its `requests/`, through `create-change`, deployed.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

The change loop is finished for an app that already exists. A request becomes merged code with no human for
one class of change (finding 29, D-26). But every app so far was set up by hand. Each of these failed at
least once and none failed legibly (OQ-15):
- the repo from the template
- the `aaas-bot` App installation
- two repository secrets
- the deployment directory
- a federated credential per environment
- the ruleset
- the public GHCR package

This list *is* the productisation surface, and §2's user cannot do any of it.

## Read first, in this order

| File | Why |
|---|---|
| `AaaS-context.md` OQ-15, OQ-17, D-14, D-26 | What provisioning has to produce, and what it must not add |
| `SETUP.md` | How the demo was bootstrapped by hand: the list to automate |
| `aaas-deployments/agent/create-app.md` §0, `.github/workflows/` | What a new repo must have before the first agent run |
| `aaas-app-template` | What a new repo starts from (conventions, `requests/`, `changes/`) |
| `FINDINGS.md` 2-3, 19, 21 | The identity failures, and what they looked like |

## State to verify before starting

- `aaas-app-demo` `master` is `bfadd80` (#22, the harness merge); `aaas-agent` `master` is `0be7b1f` (#7) or later;
  `aaas-app-template` `master` is `83c5321` (#6).
- `rg-demo-dev` does not exist; no deployment PR on `aaas-deployments` is open (#29-#31 were closed unmerged).
- No `index.lock` / `HEAD.lock` / `tmp_obj_*` in any repo's `.git`. Your local repos are pulled.
- `az` works in your terminal (this session will need it: there is no Azure login anywhere else).

## The work

1. **Inventory.** From `SETUP.md` and the demo, the exact list of what one app needs, who can create each item
   today, and what proves each one works.
2. **A provisioning script** in `aaas-deployments` (`scripts/provision-app.sh <name>`), run by you, with your
   `gh` and `az` logins:
   1. repo from the template
   2. ruleset (`test` + `build`, squash only)
   3. App installation extended to the repo
   4. repo secrets
   5. `deployments/dev/<name>/` PR
   6. federated credentials
   7. GHCR visibility
3. **Verification, separate from creation** (`scripts/verify-app.sh <name>`). For each item it asks the platform,
   not the script's own record:
   - the App can mint a token for the repo
   - the federated subject matches what a workflow presents
   - the ruleset requires the right checks
   - and so on for the rest of the list

   Every failure names the item and the fix.
4. **A second app, for real.** Provision `aaas-app-<something>`, write its first request from the template, run
   `create-change`, deploy it, then destroy it.
5. One finding; OQ-15 narrowed or closed; D-row if settled.

## Decisions I will need from you

- **Who runs provisioning.** I expect to argue: a script you run with your own `gh` and `az` logins, not a workflow.
  A workflow would need an identity that can create repos, install Apps and add federated credentials. Those are the
  widest rights in the system, sitting on the job that runs least-trusted content (the D-14 argument). Automating it
  for a product is a later, separate decision.
- **The App private key as a repo secret.** `release.yml` needs `AAAS_APP_PRIVATE_KEY` in every app repo. That is a
  secret we copy N times, against the spirit of D-14. I expect to argue: accept it for step 3, open an OQ, and look
  at an org-level secret or a token-minting service later. Do not block the step on it.
- **A second app, or a throwaway.** I expect to argue for a small real one, with a name you choose, so it gets a
  `requests/` history. Either way it is destroyed at the end.

## Done when

- `provision-app.sh` and `verify-app.sh` exist. `verify-app.sh` is green on `aaas-app-demo` and on the new app, and
  red, naming the item, when one piece is removed on purpose.
- A second app went from nothing to a running change through `create-change`, and was destroyed.

## Explicitly not this session

- Making provisioning a self-service product flow (intake, OQ-4). A third archetype. OQ-21. Anything commercial (D-19).
- The shared spec-tester blind spot (finding 29), unless the new app's first change trips over it.

## Carried over

- **The harness merge is indistinguishable from yours on GitHub** (`main0034`). It should comment on the PR: merged
  by the harness, blockers none, acceptance files unchanged. Small; do it when next in `aaas-agent`.
- **The shared blind spot** (finding 29): a "left out" rule tested at one value only (priority 1 of 1-5). A sharper
  runbook example in `write-acceptance.md` is the cheap fix.
- **Pending requests in the demo:** `item-tags`, `item-upcoming`. Old PRs #14-16 can be closed.
- **Untracked in your `aaas-agent`:** `acceptance/` and old `briefs/` are archived in `aaas-docs/evidence/` and can be
  deleted. `briefs/` is gitignored now.
- **Run drivers:** `session-1010.command` is current (it reads `session-1010-logs/QUEUE`). The older `*.command` files
  and log folders can go.
- **Policy false positives:** pipes after `dotnet` (finding 26), the `nuget` path, heredoc `fix:`, a `grep` pattern with
  `\|` (finding 28).
- **The permanent `workload_profile_name` diff**; **the operator token is admin**; **OQ-21**; **`gh pr close` refused by
  policy**; **no `.terraform.lock.hcl`**; **no ruleset on `aaas-deployments`**; **a capped run loses its work** (finding 22).
