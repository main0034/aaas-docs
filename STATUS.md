# AaaS — where things stand

Written 7 September 2026, at the end of the first working session. Start here in a new conversation.

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

Verified working: `GET /ready` → `{"database": "ok", "auth": "managed-identity"}`


## Written, not yet run

- **`agent/create-app.md`** — written for review alongside the harness, on hold until OQ-13 (it instructs the agent to use versioned migrations that do not exist). Not exercised until 4c.

## Not started

- **Phase 4c** — chaining both tasks in one job
- **Phase 5** — fix-forward
- **Phase 7** — application code generation

## Next step

Phase 4a is done. Three candidates, roughly in order of how much they unblock:

1. **OQ-13 — pick a migration tool.** It blocks Phase 7 (code generation) and
   `create-app.md` currently promises something the scaffold does not have.
2. **Phase 4c — chain create-app and create-deployment.** Needs 1 first.
3. **Merge PR #7** if you want the applied proof. Fifteen minutes, and per
   finding 8 leave it up rather than cycling create/destroy while working.

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

- **Infrastructure: destroyed.** `deployments/dev/demo/` still exists in git, so any push touching `deployments/**` will recreate the whole stack (~15 minutes, ~$20/month). Deliberate — teardown is a pause, not a deletion.
- Repos are public (needed for branch protection on the free plan)
- GHCR package `aaas-app-demo` is public, so `registry_username` is `""`
- Module is at tag `v0.2.0`
- **`aaas-agent/` is a local git repo with no remote.** Deliberate for now, but it is the only
  part of the estate with no off-machine copy, so the harness exists in exactly one place.
  Nothing in the pipeline fetches it - the Dockerfile copies `harness/` in from the local
  directory at build time - so this costs nothing functionally and everything if the laptop
  dies. When you want it up there, it is the one repo that can be private at no cost: nothing
  gates it, so it needs none of the branch protection the others were made public for.

  ```bash
  gh repo create aaas-agent --private --source=. --remote=origin --push
  ```
- **Uncommitted, on purpose:** `aaas-deployments/agent/create-app.md` (on hold until OQ-13 -
  it instructs the agent to use versioned migrations that do not exist) and
  `aaas-deployments/.gitignore` (safe to push or PR whenever)

## Things to remember

- **Never move a git tag.** Cut a new one. Terraform resolves refs at init and gives no hint a tag moved.
- **Push guardrail changes straight to master.** The `guardrails` job fails any PR touching `.github/`, `schemas/`, `scripts/`, `agent/` — including the PR that installs it.
- **Don't cancel a Terraform job.** It leaves the state lease held. `unlock.yml` recovers it.
- **Plan runs with `-refresh=false`** because the azurerm provider calls `listSecrets` on every Container App read. Don't remove it without reading finding 11.
- **Test update, not just create.** Three real bugs survived a green first apply and only appeared on the second deploy.

## Open product questions, still parked

Multi-tenant isolation, who owns the Azure tenant, drift detection, cost surfaced at request time, prod promotion, and the escape hatch — what happens when a request needs something the module does not support. That last one remains the hardest question in the idea.
