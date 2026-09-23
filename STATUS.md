# AaaS — where things stand

Written 23 September 2026, at the end of the D-22 readiness-gate session. Start here in a new conversation.

## Read these, in this order

| File | What it is |
|---|---|
| `NEXT-SESSION.md` | What the next session is for, and what to verify before starting. Read it first. |
| `STATUS.md` | This file. Current position. |
| `FINDINGS.md` | What the build actually taught us. The most valuable document here. |
| `POC-PLAN.md` | The original plan and phase structure. |
| `SETUP.md` | Runbook for bootstrap, secrets, and the deploy/destroy loop. |
| `aaas-infra-modules/modules/app-stack/README.md` | The agent-facing infrastructure contract. |
| `aaas-deployments/agent/PROMPT.md` | The agent's system prompt (written, never run). |
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

Verified working: `GET /ready` → `{"database":"ok","auth":"managed-identity","migration":"20260922174809_AddItemPriority"}`


## Written, not yet run

- **`agent/create-app.md`** — rewritten for .NET and committed. Not runnable yet: the harness image has no .NET SDK and its policy allows only `git`, `gh`, `jq` and the schema validator. Making it runnable is the first step of Phase 4c.

## Not started

- **Phase 4c** — chaining both tasks in one job
- **Phase 5** — fix-forward
- **Phase 7** — application code generation

## Next step

**Phase 4c** — make `agent/create-app.md` runnable: a .NET SDK in the harness image and a
policy that allows `dotnet`, then one run end to end. See `NEXT-SESSION.md`. After that:

- **OQ-21 — "immutable once applied", not "once merged".** Still true: fixing forward a
  merged-but-never-applied migration needs a human override. The gate made the failure visible;
  it did not make it fixable forward. Revert is the recovery that works today.
- **Teardown reliability (finding 18)** before `destroy.yml` is handed to anyone.

Running the agent again, for reference:

```bash
cd aaas-agent
export CLAUDE_CODE_OAUTH_TOKEN=...   # claude setup-token, one year, no API billing
export GH_TOKEN=...                  # fine-grained PAT, aaas-deployments only
./run.sh --request @briefs/room-booking.md
```

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

- **Infrastructure: RUNNING** since 23 September (`rg-demo-dev`, image `a708ec8`, three test rows,
  two sharing a title). ~$20/month until destroyed. `deployments/dev/demo/` pins `a708ec8`;
  `aaas-app-demo/master` is at `43d8be2`, whose migration fails against these rows — so the next
  image release from the demo will open a deployment PR that the gate turns red. That is correct
  behaviour, not a bug.
- Repos are public (needed for branch protection on the free plan)
- GHCR package `aaas-app-demo` is public, so `registry_username` is `""`
- Module is at tag `v0.3.0`, pinned by `deployments/dev/demo/main.tf`
- `aaas-agent` has a remote (`main0034/aaas-agent`)
- Merged test branches left on `aaas-deployments`: `test/gate-baseline`, `test/gate-unique`,
  `revert/unique-title`. Safe to delete.

## Things to remember

- **Never move a git tag.** Cut a new one. Terraform resolves refs at init and gives no hint a tag moved.
- **Push guardrail changes straight to master.** The `guardrails` job fails any PR touching `.github/`, `schemas/`, `scripts/`, `agent/` — including the PR that installs it.
- **Don't cancel a Terraform job.** It leaves the state lease held. `unlock.yml` recovers it.
- **Plan runs with `-refresh=false`** because the azurerm provider calls `listSecrets` on every Container App read. Don't remove it without reading finding 11.
- **Test update, not just create.** Three real bugs survived a green first apply and only appeared on the second deploy.
- **A red readiness gate means the old version is still serving.** Recover with a revert PR on the deployment, not by editing the migration (OQ-21). The PR comment names the cause.
- **A PR-scoped token cannot push `.github/workflows/`** without the Workflows permission — keep it that way for anything an agent holds (finding 19).

## Open product questions, still parked

Multi-tenant isolation, who owns the Azure tenant, drift detection, cost surfaced at request time, prod promotion, and the escape hatch — what happens when a request needs something the module does not support. That last one remains the hardest question in the idea.
