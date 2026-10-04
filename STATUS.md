# AaaS — where things stand

Written 4 October 2026, at the end of the Phase 7 session. Green was measured against hidden acceptance tests over three briefs: two correct, and the largest green-but-wrong on a boundary it never tested. The product-level plan is now `AaaS-context.md` §9, an ordered roadmap. Start here in a new conversation.

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

- **Phase 6 — the demo, not recorded by decision.** One run from a destroyed stack (finding 24): brief `item-due.md` → agent PR #11 in 5m 25s, $0.79, CI green first push → deployment PR #21 → create **failed on `appdb` "already exists"** → import recovery (#22) → readiness gate green → overdue list verified against the live app. **Prompt → running 30m 58s**, ~7m of it the failure and recovery. The cause: the app's `migrate` init container created the database before Terraform did (EF Core `Migrate()` creates a missing database). Fixed in `app-stack` v0.3.2, **not yet proven by a create**. `POC-PLAN.md` §9: eight of nine ticked, the unticked one is exactly this

- **Clean create on `app-stack` v0.3.2** (1 October, finding 25) — 14 added, no import, the database before the Container App. `POC-PLAN.md` §9 is fully ticked. 21m 46s, 18m 27s of it the Container App Environment

- **Tests that prove behaviour** (1 October, finding 25) — template #3 and demo #12: endpoint tests against CI's `postgres:16`, one cloned database per test, `--fail-skips on` and `--minimum-expected-tests 1` so a skipped or absent suite is red. Six planted route bugs all went red; the old demo suite stayed 15/15 green on the one it was named for. `AGENT.md` requires exact-row assertions with a worked example. **The agent then wrote 4 endpoint tests from a brief that never mentioned tests** (PR #13, $0.74, 7m 20s), one of them wrong; one fix round corrected it

- **Phase 7, measured** (4 October, finding 26) — briefs now end with an *Interface* section; operator-written acceptance tests, hidden from the agent, scored three runs: `item-upcoming` #14 and `item-tags` #15 (one fix round) **green-correct**, `projects` #16 **green-but-wrong** (14/15: a name valid only after trimming is refused). Fix rounds edited 0 assertions. $0.64–1.71 per brief. Calibration on #13: 4/4. Tests in `aaas-agent/acceptance/` (untracked)

Verified working on 1 October: `/ready` → `database: ok`, `managed-identity`, `AddItemDueDate` on the fresh create. On 29 September: `GET /items/overdue` and `/ready` → `migration: 20260929180049_AddItemDueDate`. On 28 September: `GET /items?q=milk&open=true` against the deployed app. Before that, on 26 September: `GET /ready` → `{"database":"ok","auth":"managed-identity","migration":"20260926142702_MarkItemDone"}`


## Not started

- **A new app from nothing** — new repo + first deployment + first app PR. Blocked on repo provisioning (OQ-15), deliberately out of scope so far

## Next step

Per `NEXT-SESSION.md` and roadmap step 2a in `AaaS-context.md` §9: **a spec-tester role.** A separate
agent session writes acceptance tests from a brief's interface without seeing any code, measured against
the operator's hidden tests and PRs #13–16. It needs no Azure and no new app runs. Competing:

- **OQ-21 — "immutable once applied", not "once merged".** Unchanged; roadmap step 4.
- **The permanent `workload_profile_name` diff** on the Container App (finding 24). Harmless so far.

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
(finding 21). `phase7.command` in the `aaas` folder is the current one: a watcher only. Started once, it
runs the harness whenever `phase7-logs/RERUN` appears (one line of `run.sh` arguments) and stops on `phase7-logs/STOP`. A RERUN written before the double-click runs at once.

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

- **Infrastructure: DESTROYED** since 1 October (`destroy` run 36908084546, green). Phase 7 did not need it,
  and no apply ran on 4 October. Nothing is billing.
- `deployments/dev/demo/` pins `app-stack` **v0.3.2** and image `0ac9d66` (`aaas-app-demo/master`: PR #13, the priority
  list), schema `AddItemDueDate`. **`0ac9d66` has never been applied**: its apply failed on the state lock and was not
  re-run before the destroy. The next `apply` (a create) is its first deployment
- `aaas-deployments` `master` has `-lock-timeout=10m` on plan and apply (`5378c12`, finding 25)
- **`aaas-app-demo` has three open, unmerged agent PRs:** #14 (`/items/upcoming`), #15 (tags, with a migration),
  #16 (projects, with a migration; hidden-test failure in a comment). Independent of each other, all off `0ac9d66`.
  Merging any of them opens a deployment PR, and its apply on a destroyed stack is a create (~22 minutes)
- All six repos are public (needed for branch protection on the free plan)
- `aaas-app-demo` `master` has a ruleset: PR required, squash only, `test` + `build` required and
  strict, no force-push or deletion (created 26 September)
- `aaas-app-template`: ruleset and repo settings now agree on squash (fixed 28 September)
- GHCR package `aaas-app-demo` is public, so `registry_username` is `""`
- Module tags: `v0.3.1` (Postgres children serialised - not the fix), **`v0.3.2`** (Container App waits for the database - proven on create, finding 25). Pinned: v0.3.2
- Three tokens in the `aaas` folder: `.claude-oauth-token-dont-delete` (the harness's Claude login), `.github_PAT_dont_delete` (operator, all repos, no Workflows, no
  Actions write) and `.aaas-agent-PAT-dont-delete` (agent: `aaas-deployments` + `aaas-app-demo`
  only; refused on `aaas-agent` and `aaas-docs`)
- Merged branches safe to delete: `recover/demo-appdb-import`, `chore/remove-appdb-import`,
  `recover/demo-appdb-import-2`, `chore/remove-appdb-import-2`, `deploy/demo-63ec008`, `deploy/demo-55b18eb`, `deploy/rooms` on `aaas-deployments`; `ci/no-fake-db` and `test/endpoint-tests` on the app and template; the agent's
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
- **A create that fails on `appdb` "already exists"** is recovered by an `import` block in the deployment directory, applied, then removed in a second PR - not by a re-run, and not by destroy (findings 23, 24). The cause was the app creating the database (finding 24); v0.3.2 fixed it (finding 25).
- **Endpoint tests skip without `TEST_POSTGRES`**, including in the agent harness. CI fails on a skip and on zero endpoint tests. To run them locally: `TEST_POSTGRES="Host=localhost;Username=postgres;Password=…" dotnet test`.
- **Read a fix round's diff for edited assertions.** A fix round will change a test's expected value until CI agrees (finding 25). Right once; it is also exactly how a real bug goes green.
- **Wait for `gate` before merging a deployment PR.** `aaas-deployments` has no ruleset, so nothing enforces it; a merge during the plan fails the apply on the state lock (finding 25).
- **Use `gh` in the linked shell, not the unauthenticated API.** `$HOME/bin/gh` with `GH_TOKEN` from `.github_PAT_dont_delete`; the shell's home is per session, so reinstall it (one tarball from the cli/cli releases, linux arm64). Unauthenticated polling hit the 60/hour limit in ten minutes (finding 25).
- **Anything the platform starts during an apply can create what Terraform expects to create.** The app is database administrator; its startup is part of the create path (finding 24).
- **`[skip ci]` in a squash-merge title stops `apply`.** Used once (#20) to pin a module on a destroyed stack. Never on a live one: `master` would describe something no apply has seen.
- **Every apply after a create shows `1 to change`** on the Container App (`workload_profile_name` → null). Known and harmless so far (finding 24).
- **Git from the linked shell leaves lock files** (`index.lock`, `HEAD.lock`, `tmp_obj_*`) — delete them after every commit, and use `GIT_OPTIONAL_LOCKS=0` for read commands.

- **Score a PR with hidden tests** in Claude's container: .NET 10 via `dotnet-install.sh --channel 10.0`, the
  distro's Postgres 16 (`initdb -A trust`), then `aaas-agent/acceptance/run-acceptance.sh <checkout> <brief>`.
  Style analyzers are switched off for the acceptance files only.
- **A brief needs an Interface section** (routes, parameters, status codes, shape) or hidden tests cannot know what to call (finding 26).

## Open product questions, still parked

Multi-tenant isolation, who owns the Azure tenant, drift detection, cost surfaced at request time, prod promotion, and the escape hatch — what happens when a request needs something the module does not support. That last one remains the hardest question in the idea.
