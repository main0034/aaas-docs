# AaaS POC — E2E Agentic Application Delivery

**Target capability:** a non-technical user describes an application. An agent builds the app code, selects appropriate infrastructure, opens PRs to both an application repo and a deployment repo, and the merged result is a running, reachable application in your Azure tenant.

**POC scope:** the full chain, with app-code *generation* stubbed out until last. One Azure subscription, one `dev` environment, one stack shape — Postgres Flexible Server + Container Apps. Out of scope: multi-tenancy, billing, prod promotion, cost controls, the business model.

---

## 1. The chain

```
  user request
       │
       ▼
  ┌─────────────────────────────────────────────┐
  │ AGENT                                       │
  │  • interprets request                       │
  │  • creates app repo from template           │
  │  • writes app code           ── PR ──►  app repo
  │  • selects infra shape                      │
  └─────────────────────────────────────────────┘
                                              │ merge
                                              ▼
                                    app CI: build + push image
                                    ghcr.io/<org>/<name>:<sha>
                                              │
                                              │ automated PR
                                              ▼
                                        deployments repo
                                              │ merge
                                              ▼
                                    terraform apply (OIDC)
                                              │
                                              ▼
                                    running app + Postgres
```

Two agent-authored PRs, two human merges, one running application. Everything between the merges is deterministic CI.

### The two agent surfaces are not the same, and shouldn't be governed the same way

This is the central design insight, and it's worth being explicit about because it determines where you spend your engineering effort.

| | **Infrastructure** | **Application code** |
|---|---|---|
| Agent writes | one validated JSON file | arbitrary source files |
| Correctness model | **constrained choice** | **tested** |
| Guardrails | JSON Schema enums, Terraform `validation` blocks, OPA/checkov policy | build must succeed, tests must pass, health check must respond |
| Failure mode | invalid input, caught pre-plan | subtly wrong behaviour, caught by tests or not at all |
| Blast radius | your Azure bill and tenant | one container |

Trying to make app-code generation template-shaped will fail — that's not what code is. Trying to make infra generation free-form will produce an agent that can open a Postgres server to the internet. Keep them separate, and put your guardrail effort into the infra side, where mistakes are expensive and constrainable.

---

## 2. Repositories

### `aaas-infra-modules`
Reusable Terraform, versioned by git tag. Consumed by ref, never edited by the agent.

```
modules/app-stack/     main.tf variables.tf outputs.tf versions.tf README.md
examples/minimal/
.github/workflows/validate.yml
```

### `aaas-app-template`
A GitHub **template repository**. The starting point for every generated app. Must be immediately buildable and deployable *as-is* — a working app that happens to do very little.

```
app/
  main.py              # FastAPI: GET /health, GET /items backed by Postgres
  requirements.txt
tests/
  test_health.py       # runs in CI, no DB needed
Dockerfile             # multi-stage, non-root, healthcheck
.github/workflows/
  ci.yml               # lint, test, build image, push to GHCR
  release.yml          # on main: push image, then PR to deployments repo
AGENT.md               # conventions the agent must follow when editing this repo
```

`AGENT.md` matters more than it looks. It's the contract that keeps generated code consistent: where routes go, how config is read, how DB access is done, what must never be edited (the Dockerfile, the workflows). Treat it as part of the product.

### `aaas-app-<name>` (one per generated application)
Created by the agent from the template. This is the model that matches the end product — clean isolation, per-app CI, per-app history, and a repo you could hand to a customer.

### `aaas-deployments`
The state of the world. One directory per deployment. Data, not code.

```
deployments/dev/<name>/
  terraform.tfvars.json    # agent writes; app CI bumps container_image
  backend.hcl
  main.tf                  # module block, pinned to a module tag
schemas/app-stack.schema.json
agent/PROMPT.md  agent/create-app.md  agent/create-deployment.md
.github/workflows/plan.yml  apply.yml  destroy.yml
```

---

## 3. The image handoff

The single most failure-prone link in the chain. Build it early (Phase 4b) and in isolation.

**Mechanism:** on merge to `main` in an app repo, `release.yml`:

1. Builds and pushes `ghcr.io/<org>/aaas-app-<name>:<git-sha>` — **immutable tag, always the SHA.** Never `latest`; the deployments repo must record exactly what is running.
2. Checks out `aaas-deployments`.
3. `jq` edits `deployments/dev/<name>/terraform.tfvars.json`, setting `container_image` to the new ref.
4. Opens a PR titled `chore(<name>): deploy <sha7>`, body containing the app-repo commit message and a compare link.

**Auth for step 2–4:** a GitHub App installed on the org, with `contents: write` and `pull_requests: write` on the deployments repo. Do *not* use a personal access token — a GitHub App scopes cleanly, and it's the thing you'd ship. Store the app ID and private key as org-level secrets in the app repos.

**Why a PR and not a direct push:** the deployment PR is the audit record. Merging it is the moment infrastructure changes, and it runs through the same `plan` gate as everything else. In the eventual product this PR is also the natural place to surface a cost delta and a plain-language summary to a non-technical user — "this will add £14/month and restart your API." That framing only works if the deploy is a reviewable object.

**Auto-merge:** for the POC, merge by hand. Later, `gh pr merge --auto` on image-bump-only PRs (validated by checking the diff touches exactly one key) is the obvious first automation.

---

## 4. The `app-stack` module

One opinionated module, small input surface, everything else derived.

**Agent-facing inputs:** `name`, `environment`, `location`, `container_image`, `cpu`/`memory`, `min_replicas`/`max_replicas`, `postgres_sku`, `postgres_storage_mb`, `postgres_version`, `app_env` (non-secret), `tags` (must include `owner`, `costCenter`).

Put a `validation` block on every single one, with explicit allowed sets for SKUs and sizes. This is your cheapest and most effective guardrail — it rejects bad agent output before Terraform plans and long before Azure sees it.

**Resources:**
- Resource group `rg-<name>-<env>`
- VNet with two delegated subnets — Container Apps infra (`/23` min, delegated to `Microsoft.App/environments`) and Postgres (delegated to `Microsoft.DBforPostgreSQL/flexibleServers`)
- Private DNS zone `privatelink.postgres.database.azure.com`, VNet-linked **before** server creation
- `azurerm_postgresql_flexible_server`, private access only, no public endpoint; generated admin password stored in Key Vault
- `azurerm_postgresql_flexible_server_database`
- Key Vault + connection-string secret
- User-assigned managed identity; `Key Vault Secrets User` role assignment
- Log Analytics workspace + VNet-injected `azurerm_container_app_environment`
- `azurerm_container_app` with the UAMI attached and the connection string injected as a Key Vault reference
- GHCR pull secret (GHCR chosen over ACR to keep the module smaller for the POC)

**Outputs:** app FQDN, Postgres FQDN, resource group, Key Vault URI.

Pin `required_version = "~> 1.9"` and `azurerm ~> 4.x` with an exact floor. Floating provider versions make a deterministic pipeline look flaky.

**Known time sinks** — budget for these; they are the real cost of Phase 1:
- Postgres delegated subnet must be empty and cannot be changed after creation.
- If the private DNS zone link isn't in place first, the app silently resolves the public name and fails to connect.
- Postgres Flexible Server deletion is slow, which makes iteration painful — keep a long-lived resource group during development and recreate only the container app.

---

## 5. Authentication

**GitHub → Azure (deployments repo):** OIDC, no stored secrets.

1. App registration `sp-aaas-deploy-dev` + service principal.
2. Two federated credentials: `repo:<org>/aaas-deployments:pull_request` (plan) and `repo:<org>/aaas-deployments:ref:refs/heads/main` (apply). Audience `api://AzureADTokenExchange`.
3. RBAC: `Contributor` + `User Access Administrator` on the subscription — the latter because the module creates role assignments. Scope down after the POC.
4. Workflow permissions: `id-token: write`, `contents: read`.
5. Terraform: `use_oidc = true` on provider and `azurerm` backend; `use_azuread_auth = true` on the backend, with `Storage Blob Data Contributor` for the SP.
6. Repo *variables* (not secrets): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

Worth doing at the start: **two** service principals — plan gets `Reader`, apply gets write. Cheap now, awkward to retrofit.

**Agent → GitHub:** the agent needs `repo` scope to create repos from the template and open PRs. During the POC, your own `gh` login is fine. Before anything runs unattended, move to a GitHub App with a scoped installation.

**State backend:** dedicated storage account, bootstrapped out-of-band by a small script in the modules repo (chicken-and-egg). One container, key `dev/<name>.tfstate`, blob versioning + soft delete on, locking via blob lease.

---

## 6. Workflows

**`ci.yml`** (app repo, on PR): lint, unit tests, `docker build` (no push). This is the app-side guardrail — the agent's code must compile and pass tests before a human sees it.

**`release.yml`** (app repo, on push to `main`): build, push `:<sha>` to GHCR, then open the image-bump PR against `aaas-deployments` as described in §3.

**`plan.yml`** (deployments repo, on PR touching `deployments/**`):
1. Detect changed deployment directories.
2. Validate `terraform.tfvars.json` against the JSON Schema — **fail fast here.**
3. Policy gate: conftest/OPA or checkov. Deny public Postgres, missing required tags, non-SHA image tags.
4. `init -backend-config=backend.hcl` → `fmt -check` → `validate` → `plan -out=tfplan`.
5. Comment the plan on the PR; upload the plan artifact.

**`apply.yml`** (deployments repo, on push to `main`): re-plan and apply; comment the app URL back on the merged PR. Gate behind a GitHub Environment named `dev` so a required reviewer can be added later without touching the workflow.

**`destroy.yml`**: manual dispatch, takes a deployment name. Build this in Phase 2, not later — see §8.

**`validate.yml`** (modules repo): fmt, validate, tflint, checkov, plan against `examples/minimal`; tag on merge.

---

## 7. The agent

**Runtime for the POC:** Claude Agent SDK or Claude Code, running locally with checkouts and `gh` authenticated. Hosting it (issue-triggered, or behind a web form for non-technical users) is a later concern — it changes *where* the agent runs, not what it does.

**Tools:** `Read`, `Write`, `Bash` restricted to `git`, `gh`, `jq`, `ajv`, and the app's test runner. Deliberately **no** `terraform` and **no** `az` — the agent cannot touch your tenant. Its only path to production is a PR that CI applies.

**Task: create an application**
1. Interpret the request. Ask about anything material that's missing — what the app does, who owns it, expected traffic. Never guess at `owner`/`costCenter` tags.
2. `gh repo create aaas-app-<name> --template aaas-app-template --private`
3. Read `AGENT.md`. Write the app code on a branch, following its conventions.
4. Run tests locally until green. **Do not open a PR with failing tests.**
5. Open the app PR, body describing what was built in plain language.

**Task: create the deployment**
1. Read `schemas/app-stack.schema.json` and `modules/app-stack/README.md`. These are the source of truth — never invent a variable.
2. Choose infra parameters from the allowed sets, justified by the request (a low-traffic internal tool gets `B_Standard_B1ms` and `min_replicas = 0`).
3. Scaffold `deployments/dev/<name>/` from the template; `container_image` is a placeholder until the first app build.
4. Validate: `ajv validate -s schemas/app-stack.schema.json -d .../terraform.tfvars.json`. Iterate until clean.
5. Open the deployment PR summarising what's being deployed, estimated monthly cost, and assumptions made.

**Task: fix-forward** — if `ci.yml` or `plan.yml` fails, read the failure from the PR comment or Actions log, correct the source, push. This is the capability that actually distinguishes an agentic pipeline from a self-service form: a form cannot repair its own output. Worth building deliberately rather than hoping it emerges.

**Guardrails, ranked by value per hour spent:**
1. Terraform `validation` blocks on every variable
2. JSON Schema with `enum`s on every constrained field
3. **CODEOWNERS on `modules/**`, `schemas/**`, `.github/**`, and `AGENT.md`** — the agent must never be able to alter its own guardrails. Day one.
4. Branch protection: no direct push to `main` in any repo
5. OPA/checkov policy gate
6. App CI tests as the app-side equivalent of schema validation

---

## 8. Phases

| # | Phase | Outcome | Est. |
|---|---|---|---|
| 0 | Bootstrap | Repos created, state storage account, SP + federated credentials, OIDC hello-world workflow running `az account show` | 0.5 d |
| 1 | Module | `app-stack` applies from your laptop; container app reaches Postgres over private DNS | 1–2 d |
| 2 | Infra pipeline | Hand-written tfvars → PR → plan comment → merge → applied → destroy works. **No agent.** | 0.5–1 d |
| 3 | Guardrails | JSON Schema, validation blocks, checkov/OPA, CODEOWNERS, branch protection | 0.5 d |
| 4a | Agent: infra | Prompt → deployment PR → merged → running app on a fixed public sample image | 1 d |
| 4b | **Image handoff** | Template repo + one hand-made app repo. Trivial change → merge → image built → auto-PR to deployments → merged → new revision live. **Still no code generation.** | 1 d |
| 4c | Chain | Agent runs both tasks as one job. Two PRs, two merges, one running app from a fixed template. | 0.5 d |
| 5 | Fix-forward | Agent reads a failed plan or failed test and self-corrects | 0.5–1 d |
| 6 | Demo | Recorded E2E run; written notes on what broke and what it implies | 0.5 d |
| 7 | **Code generation** | Agent writes real app code from a natural-language description | 2 d+ |

**~9–11 days to Phase 6; code generation on top.**

Two sequencing rules that are worth holding to:

- **Phase 2 before Phase 4a.** If the agent arrives before the pipeline is proven, every failure is ambiguous and you lose days deciding whether the agent or the plumbing is at fault.
- **Phase 4b before Phase 7.** The image handoff is the most brittle link in the chain and has nothing to do with code generation. Prove it with a hand-written app and a one-line change. Then, when generated code fails, you know it's the code.

Phase 7 is deliberately last and deliberately open-ended. By that point everything around it is proven, so it's the only variable — which is exactly the position you want to be in when you start assessing whether agent-written applications are actually good enough for the product.

---

## 9. Acceptance criteria

- [ ] One natural-language request produces two PRs, in the right repos, with no hand-written code or Terraform.
- [ ] App CI runs tests and builds an image tagged with the git SHA.
- [ ] Merging the app PR automatically produces a deployment PR bumping `container_image`.
- [ ] Merging the deployment PR produces a reachable HTTPS URL that successfully queries Postgres.
- [ ] Postgres has no public endpoint; no credential appears in git, in plan output, or in logs.
- [ ] A malformed request (bad SKU, missing owner tag) is rejected by schema or module validation — not by Azure.
- [ ] A deliberately broken app change is caught by app CI before it reaches an image.
- [ ] `destroy.yml` cleanly removes an entire deployment.
- [ ] Wall-clock from prompt to running application is measured and recorded.

---

## 10. Decide before Phase 0

- **Azure region** — `swedencentral` assumed.
- **GitHub org** — needed for GHCR paths and the GitHub App installation. A dedicated org keeps generated repos out of your personal namespace.
- **Sample app shape** — FastAPI + Postgres assumed. Whatever you pick becomes the template, and the template shapes what the agent can generate. Worth 30 minutes of thought, not more.
- **Azure subscription** — a dedicated one is worth it for clean teardown and honest cost visibility.
- **Naming convention** — lock `<prefix>-<name>-<env>-<region>` now; renaming later forces resource recreation.

---

## 11. Deliberately unanswered

Parked, but logged so they aren't rediscovered as surprises:

- Multi-tenant isolation — per-customer subscription vs resource group vs state key
- Who owns the Azure tenant in the real product, you or the customer
- Drift detection when someone edits resources by hand
- Cost estimation and guardrails at request time, surfaced in language a non-technical user can act on
- Prod promotion, approvals, change windows
- **The escape hatch:** what happens when a user asks for something the module doesn't support. This is the hardest product question in the whole idea — a fixed module is what makes the infra side safe, and it's also the ceiling on what the product can build. Worth returning to once the POC has told you how often you hit that ceiling in practice.
