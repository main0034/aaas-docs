# AaaS — where things stand

Written 28 September 2026, at the end of the Phase 5 session: the agent fixed its own failed check. Start here in a new conversation.

## Read these, in this order

| File | What it is |
|---|---|
| `NEXT-SESSION.md` | What the next session is for, and what to verify before starting. Read it first. |
| `STATUS.md` | This file. Current position. |
| `FINDINGS.md` | What the build actually taught us. The most valuable document here. |
| `POC-PLAN.md` | The original plan and phase structure. |
| `SETUP.md` | Runbook for bootstrap, secrets, and the deploy/destroy loop. |
| `aaas-infra-modules/modules/app-stack/README.md` | The agent-facing infrastructure contract. |
| `aaas-deployments/agent/PROMPT.md` | The agent's system prompt. |
| `aaas-agent/README.md` | The harness: how to run it, and what the boundary actually is. |
| `aaas-app-template/AGENT.md` | Conventions for application code. |

## Done and proven

- **Phase 0** — bootstrap, OIDC (three federated credentials), state backend, $20 budget alert
- **Phase 1** — `app-stack` module builds a working private-networked app: Postgres with no public endpoint, private DNS, Container Apps, managed identity
- **Phase 2** — PR → schema validation → plan → merge → apply → destroy, with split read/write identities and no stored Azure credential
- **Phase 3** — JSON Schema, Terraform validation blocks, CODEOWNERS, `guardrails` job, `gate` check
- **Phase 4b** — cross-repo image handoff proven for **both** create and update: app merge → image build → automated deployment PR → applied revision
- **Phase 4a** — the agent produces infrastructure: one plain-language request → PR #7, `Plan: 14 to add, 0 to change, 0 to destroy`, 4m 11s, no human edit to the tfvars (finding 14)
- **Passwordless database auth** — Entra-only Postgres; no credential in state, Key Vault, a Container App secret, or the repo
- **.NET 10 scaffold (D-20)** — template and demo both on ASP.NET Core minimal APIs, EF Core, xUnit v3; CI adds a format check, warnings-as-errors, a model-vs-migration check and two migration guards
- **Schema migrations (D-21)** — EF Core migrations applied by a `migrate` init container (`app-stack` v0.3.0), proven in Azure on create and on update, with the identity token working in the init container
- **A failing migration is safe** — one transaction, rolled back whole, old revision keeps serving, data untouched (finding 16)
- **Post-apply readiness gate (D-22)** — every apply is followed by `scripts/readiness_gate.sh`, which asks Container Apps whether the new revision is running, not the app. Proven red on a live-data migration failure (cause posted on the PR), green on create and on the revert that recovered it (finding 19). A green deploy now means the application runs
- **Teardown no longer hangs on Postgres child resources** — `destroy.yml` drops them from state first and deletes the server directly; a full destroy succeeded in 25 minutes (finding 20). It still does not verify that anything was removed

- **Phase 4c — the agent writes the application.** Harness image has the .NET SDK (10.0.401); the policy allows the runbook's `dotnet` commands and refuses the rest; `--task create-app --app-repo <name>`. Two runs against `aaas-app-demo` (finding 21): **prompt → running app in 8m 23s for an update, 18m 43s when the stack had to be created.** Agent cost $0.43–0.90 (shadow) per run. CI green on first push both times; the second run had zero policy refusals

- **Phase 5 — fix-forward.** `--fix-rounds 2`: the harness waits for the PR's checks on its head commit and, on red, gives a *fresh* session the request, diff stat and trimmed failure log. One run went red (`test`: an untranslatable EF query → 500), was fixed in one round, and reached a green readiness gate: **$0.38 per round, 2m 47s + 2m 38s of agent time, prompt → running 26m 06s** including a failed create and its recovery (finding 23). The CI check banning a fake database in tests is on `master` in the app and the template

Verified working on 28 September: `GET /items?q=milk&open=true` against the deployed app. Before that, on 26 September: `GET /ready` → `{"database":"ok","auth":"managed-identity","migration":"20260926142702_MarkItemDone"}`


## Not started

- **A new app from nothing** — new repo + first deployment + first app PR. Blocked on repo provisioning (OQ-15), deliberately out of scope so far
- **Phase 7** — application code generation

## Next step

**Phase 6 — the recorded demo**, per `NEXT-SESSION.md`. Candidates that compete with it:

- **OQ-21 — "immutable once applied", not "once merged".** Still true: fixing forward a
  merged-but-never-applied migration needs a human override. Revert is the recovery that works today.
- **Post-destroy assertion** — `az group exists` must be `false` after a destroy, or the run fails (finding 20). About five lines.
- **Postgres child resources on create** (finding 23) — the fourth failure of that shape. Recovery was an `import` block; nothing automatic exists.

Running the agent again, for reference:

```bash
cd aaas-agent
export CLAUDE_CODE_OAUTH_TOKEN=...   # claude setup-token, one year, no API billing
export GH_TOKEN=...                  # fine-grained PAT: deployments + the app repo, never Workflows
./run.sh --request @briefs/room-booking.md                        # create-deployment
./run.sh --task create-app --app-repo aaas-app-demo \
         --request @briefs/item-done.md --non-interactive          # create-app
./run.sh --task create-app --app-repo aaas-app-demo \
         --request @briefs/item-search.md --fix-rounds 2           # with fix-forward
```

From a linked cloud session, Claude cannot type into Terminal (click-only) and the linked
shell has no Docker: the run goes in a script Martin starts once, logged through `script(1)`
(finding 21). `phase5/phase5.command` in the `aaas` folder is the current one: started once, it
re-runs the harness whenever `phase5/logs/RERUN` appears and stops on `phase5/logs/STOP`.

`runs/<id>/report.md` carries the wall clock, the tool histogram, the cost
breakdown and every policy refusal. Read the refusals: a refusal that recurs is
a runbook problem, not an agent problem, and that is the cheapest improvement
available. One of the two in the first run was exactly that.

## Decisions taken when building the harness

| Decision | Why |
|---|---|
| Claude Agent SDK (Python), not Claude Code CLI | Programmatic hooks, and per-turn timing that makes "how long does prompt-to-PR take" a measurement rather than a stopwatch |
| Separate `aaas-agent` repo | The agent has no checkout of the thing that constrains it. Runbooks stay in `aaas-deployments/agent/` where it reads them, under CODEOWNERS |
| Runs in a container, cloning from origin | The agent never touches a local working tree. A bad run costs a directory and a branch |
| `python3` restricted to `scripts/validate_deployment.py` | It has to run the validator; anything that runs Python can `pip install azure-cli`. Restricting it to one script closes the widest hole for one regex |
| `verify-isolation` runs on every start | No `az`, no `terraform`, no `AZURE_*`/`ARM_*`, no `~/.azure` — asserted, not assumed. Finding 2 cost a day to an unverified assumption about identity |

**The tool allowlist is not a security boundary, and the code says so.** What
contains the agent is that no Azure credential exists in the container and no
`az`/`terraform` binary is installed. Trusting the allowlist instead would be
trusting the wrong layer.

## Current state of the environment

- **Infrastructure: RUNNING** unless Martin has run `destroy` since (recreated 28 September by
  deployment PR #17, recovered by #18) — ~$20/month, dominated by Postgres. Destroy with
  `destroy.yml` from the GitHub UI (the operator token gets 403 on `workflow_dispatch`); then check
  that the URL no longer resolves and, from a machine with `az`, `az group exists -n rg-demo-dev`
  prints `false` (finding 20).
  URL: `https://ca-demo-dev.bravemeadow-0aa9b7fc.swedencentral.azurecontainerapps.io` — **a new
  domain**: the environment was recreated, so the old `mangoglacier` URL is gone for good
- `deployments/dev/demo/` pins image `7f382fa` (`aaas-app-demo/master`: item search), schema
  `MarkItemDone` (search needed no migration)
- All six repos are public (needed for branch protection on the free plan)
- `aaas-app-demo` `master` has a ruleset: PR required, squash only, `test` + `build` required and
  strict, no force-push or deletion (created 26 September)
- `aaas-app-template`: ruleset and repo settings now agree on squash (fixed 28 September)
- GHCR package `aaas-app-demo` is public, so `registry_username` is `""`
- Module is at tag `v0.3.0`, pinned by `deployments/dev/demo/main.tf`
- Two tokens in the `aaas` folder: `.github_PAT_dont_delete` (operator, all repos, no Workflows, no
  Actions write) and `.aaas-agent-PAT-dont-delete` (agent: `aaas-deployments` + `aaas-app-demo`
  only; refused on `aaas-agent` and `aaas-docs`)
- Merged branches safe to delete: `recover/demo-appdb-import`, `chore/remove-appdb-import`,
  `deploy/rooms` on `aaas-deployments`; `ci/no-fake-db` on the app and template; the agent's
  `feat/*` branches on `aaas-app-demo` (not `feat/notebook` — PR #6, never merge)

## Things to remember

- **Never move a git tag.** Cut a new one. Terraform resolves refs at init and gives no hint a tag moved.
- **Push guardrail changes straight to master.** The `guardrails` job fails any PR touching `.github/`, `schemas/`, `scripts/`, `agent/` — including the PR that installs it.
- **Don't cancel a Terraform job.** It leaves the state lease held. `unlock.yml` recovers it.
- **Plan runs with `-refresh=false`** because the azurerm provider calls `listSecrets` on every Container App read. Don't remove it without reading finding 11.
- **Test update, not just create.** Three real bugs survived a green first apply and only appeared on the second deploy.
- **A red readiness gate means the old version is still serving.** Recover with a revert PR on the deployment, not by editing the migration (OQ-21). The PR comment names the cause.
- **After a failed destroy, destroy again — never apply first.** The destroy drops the Postgres child resources from state; an apply would try to recreate them and fail on the database (finding 20).
- **Never use `az group delete --no-wait` while pipeline jobs may run.** Wait until `az group exists -n <rg>` prints `false`.
- **A PR-scoped token cannot push `.github/workflows/`** without the Workflows permission — keep it that way for anything an agent holds (finding 19). Probed on 26 September: 403.
- **Merge on green checks, checked by name.** The ruleset enforces it on `aaas-app-demo` now; the operator token is admin, so do not rely on the ruleset alone.
- **A run sees only its own directory.** `run.sh` mounts `runs/<id>`, not `runs/`; a run that could read earlier runs copied their code (finding 23).
- **A create that fails on a Postgres child with "already exists"** is recovered by an `import` block in the deployment directory, applied, then removed in a second PR - not by a re-run, and not by destroy (finding 23).
- **Git from the linked shell leaves lock files** (`index.lock`, `HEAD.lock`, `tmp_obj_*`) — delete them after every commit, and use `GIT_OPTIONAL_LOCKS=0` for read commands.

## Open product questions, still parked

Multi-tenant isolation, who owns the Azure tenant, drift detection, cost surfaced at request time, prod promotion, and the escape hatch — what happens when a request needs something the module does not support. That last one remains the hardest question in the idea.
