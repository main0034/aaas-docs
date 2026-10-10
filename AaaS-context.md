# AaaS — Application as a Service

**Status:** v1 platform decided · POC pipeline working · agent writes infrastructure *and* application PRs · prompt → running change in ~8 minutes · deploys gated on the app actually running · agent fixes its own failed checks · full chain run from a destroyed stack · clean create proven · green means routes returned the right rows · green measured against hidden tests: right 2 of 3 · a spec-tester that never sees the code caught the miss · every change carries its request and hidden acceptance tests in the app repo, enforced by CI · the harness merged its first change without a human  
**Owner:** Martin Ingeson · **Last updated:** 2026-10-10 (v0.19)

This is the living context document for the AaaS product. It is updated across
conversations. Decisions move from *Open Questions* to *Decisions* as they are settled.

---

## Current position (September 2026)

A working Azure pipeline exists. The detailed record lives in the repository at
`~/Documents/Programmering/aaas/aaas-docs`; those documents are the operational source of truth and
this one stays at product level.

| File | What it is |
|---|---|
| `STATUS.md` | Where the build stands and what the next step is |
| `FINDINGS.md` | What the build actually taught us. The most valuable of the four |
| `POC-PLAN.md` | The phase structure |
| `SETUP.md` | Bootstrap, secrets, and the deploy/destroy runbook |

**Proven end to end.** A pull request produces a schema-validated Terraform plan; merging
applies it; the result is a reachable HTTPS application talking to a Postgres server with no
public endpoint and no password. The cross-repo image handoff works for both create and
update. Since 2026-09-23 a green deploy means the new revision is actually running: a
readiness gate after the apply turns the pipeline red when it is not, and names the cause on
the deployment PR (D-22).

**Proven on 2026-09-26 (Phase 4c).** The agent writes application code: a plain-language request
became a migration, two endpoints and tests, through CI, image, deployment PR and readiness gate,
with no human code edits. **Prompt → running: 8m 23s for a change to an existing app, 18m 43s
when the stack had to be created**, of which the agent itself was 2m 43s–4m 11s and $0.43–0.90
(shadow API cost). The rest is CI and Azure.

**Proven on 2026-09-28 (Phase 5).** Fix-forward: a PR went red on a real bug (a query EF Core could
not translate, which would have been a 500 in production); the harness handed the trimmed log to a
fresh session, which fixed it in one round and reached a green readiness gate. **$0.38 per round,
the same as the original push** — budget a fix round as one more run (D-23).

**Proven on 2026-09-29 (Phase 6).** The whole chain from a destroyed stack in one sitting: request →
agent PR (5m 25s, $0.79, green first push) → deployment PR → create → readiness gate → the feature
checked against the live app, **30m 58s**. Except the create: it failed on the database and needed an
`import` again. The cause was the application - its migration step created the database before
Terraform could (OQ-14). Fixed in the module (`app-stack` v0.3.2).

**Proven on 2026-10-01.** A clean create on v0.3.2: 14 resources, no import, ~22 minutes (18 of them the
Container App Environment). And **tests that prove behaviour** (D-24): the app's endpoint tests run
against a real Postgres in CI, and an agent briefed without any mention of tests wrote four that assert
exactly which rows come back. One of them was wrong; a fix round corrected the *assertion*, which was
right that time and is how a real bug would go green another time (OQ-5).

**Measured on 2026-10-04 (Phase 7).** Is green correct? Acceptance tests written from each brief before the
run, never visible to the agent, run against its PR after CI. Small and medium briefs (a filtered route; tags
with a migration, a join table and a red-then-fixed round): **correct**. The large brief (a second entity with
progress and delete rules): **green but wrong**. A boundary the brief stated ("100 characters after trimming")
was validated before trimming, and none of the agent's 20 tests covered it. Its tests were right; one was
missing. Fix rounds edited no assertions. $0.64-1.71 per brief.

**Measured on 2026-10-04, afternoon (finding 27).** A spec-tester role. A separate agent session writes
the acceptance tests from the brief and physically cannot see the change: it gets `master` only, with no
remote, no token and no `gh`. Over five runs on four PRs: **no false reds, both real defects caught (the
#16 miss included), 15 of 17 planted bugs**, at $0.42-0.77 a run. Both runs on the largest brief missed
the same rule. The writers share the runbook's blind spots, so the runbook is where the next gain is.

**Built on 2026-10-10 (finding 28, D-25).** The change record. Every change gets `changes/<id>/` in its app repo:
the request, and acceptance tests a spec-tester wrote from it before any code existed. The builder works without
seeing them; the harness commits them after its push, and CI runs them as part of the required `test` check, then
on every later change. Two changes went through it green on first push ($2.10-2.34, 15-17 minutes). But one
spec-tester run in three passed #16's known defect, so a single run is not yet a gate to merge on unseen.

**Proven on 2026-10-10, evening (finding 29, D-26).** A merge without a human. Two independent spec-testers
per change catch 95% of planted bugs and every real defect so far, against 90% for one. On that basis the harness
merged `item-calendar` (#22): request in the repo → two spec-testers → builder → green on first push →
merged, 16 minutes, $2.22, nobody reading the code. Only a class of change may do this: no migration, no
dependency, nothing outside `src/`, `tests/` and its own record. What still gets through is a blind spot every
spec-tester run shares.

**Not proven.** A new application from nothing (repo provisioning, OQ-15).

**This document drifted from the build.** The POC documents do not reference it, which is how
five weeks of Azure-specific work happened while §9 still said "spike Scaleway first". §9 is now
the ordered roadmap and `NEXT-SESSION.md` points at a line of it.
Sections 3.3, 4.1, 4.2 and 4.3 were corrected on 2026-09-20 and the decisions below now
reflect what exists. Keeping them in step is a per-session obligation, not an occasional one.

---

## 1. Vision

A service that lets a small or medium business get a real, running application —
web frontend, simple backend, data storage — by describing what they need in plain
language. Agents translate that description into code and infrastructure, commit it
to a repository, and deploy it to Azure.

The differentiator is **not** the code generation. It is **standardization**: every
application produced is built from the same small set of vetted Terraform modules and
application archetypes. This keeps the estate operable by a small team, keeps cost
predictable, and keeps security/compliance a property of the platform rather than of
each generated app.

**One-line pitch:** *Describe your app; we run it in your Azure, on a standard we maintain.*

### 1.1 Competitive landscape

Five groups. Only two matter.

| Group | Examples | Threat |
|---|---|---|
| AI app builders | Lovable, Bolt, v0, Replit, Base44 | High on the *creation* moment, low on operations |
| No-code SMB platforms | Knack, Zite, Glide, Bubble | Low — proprietary runtime, different buyer |
| Microsoft Power Platform | Power Apps "Plans", Copilot Studio | **Highest.** Same pitch, same cloud, bundled |
| Agentic IaC / IDP tooling | Azure Deployment Agent, StackGen | Not competitors — components to reuse |
| Agencies and MSPs | local custom-software shops | The incumbent whose customers we take |

Market context: AI app builders are roughly a $4.7B market with ~63% non-developer users —
the persona thesis is validated by other people's revenue. Lovable raised $330M at a $6.6B
valuation; Replit went $10M→$100M ARR in nine months. We cannot outspend any of them.

**The gap.** Every player in group 1 is a self-serve tool that stops at "it works on our
hosting." None of them *operate* what they generate. The industry question in 2026 moved
from "can AI build apps?" to "can AI get apps to production?" — that gap is the wedge.

**Power Platform is the real threat.** "Plans" runs a multi-agent flow from natural language
to Dataverse tables, apps, flows and agents. It is on Azure, bundled, and the SMB already
pays Microsoft. Our counter-arguments: Dataverse lock-in, per-user licensing that scales
badly, it needs a tenant admin and a competent "maker", Power Pages is weak for genuinely
public-facing apps, and we produce real portable code. That has to compress into one
sentence a skeptical buyer accepts.

**Two consequences.**

1. **Do not compete on code generation.** It is the most contested, best-capitalized,
   fastest-commoditizing layer in the stack. Wrap an existing engine. The moat is the
   archetype catalog, the operational standard, and the customer relationship.
2. **The hosting platform must be a reason to choose us, not an incidental detail.**
   See §3.3.

### Non-goals (v1)

- Arbitrary architectures. If a request does not fit an archetype, it is out of scope
  or becomes a new archetype through a deliberate process.
- Migrating or absorbing existing customer applications.
- Multi-cloud. Azure only.

---

## 2. Target user

**Primary:** a non-technical business user at an SMB — owner, ops lead, office manager.
They know their process and their data. They do not use git, do not open the Azure
portal, and cannot evaluate a Terraform plan.

Implications that constrain every later design decision:

- The user-facing surface is a **conversation plus a preview**, never a repo or a portal.
- Git, Terraform, and pipelines are internal machinery the user should never see.
  They still exist — they are how *we* stay safe and auditable.
- Every destructive or cost-increasing action needs either a guardrail that makes it
  impossible, or an approval step phrased in business terms ("this will cost about
  X per month" — not "this will create an Elastic Pool").
- Rollback must be one button. The user will break things.

**Secondary user:** us — the operator. We need to see all customer environments, drift,
cost, and failures in one place.

---

## 3. Tenancy and ownership model

Applications are deployed into **our Azure tenant**, with **one subscription per customer**
under a shared management group.

| Concern | Owner |
|---|---|
| Azure tenant, subscriptions, billing | Us |
| Customer data (we are the processor, they are the controller) | Customer controls, we process |
| Terraform modules, archetypes, agent platform | Us |
| Application source repository | Us, with a defined handover path (see §3.2) |
| Day-2 operations, patching, module upgrades | Us, via the standard |

**Why our tenant:** the primary persona cannot grant a service principal rights over an
Azure subscription. Requiring that would put an IT gatekeeper between us and every sale.
Self-hosting reduces onboarding to a signup form.

### 3.1 Why subscription-per-customer, not resource-group-per-customer

Subscriptions are free to create and give us, as enforced boundaries rather than
conventions:

- **Cost splitting is native.** Azure Cost Management reports per subscription with no
  tag hygiene required. Tag-based cost allocation rots the moment one module forgets a tag.
- **Quotas and service limits are per subscription**, so one customer cannot exhaust
  another's headroom.
- **Blast radius.** A bad apply, a leaked credential, or a policy exemption is contained
  to one customer.
- **Exit path.** Azure subscriptions can be transferred to another billing account or
  tenant. Co-mingling customers destroys this option permanently.

Isolation is enforced by **Azure Policy at the management group level** — SKU allowlists,
mandatory diagnostics, deny public network access — not by trusting each Terraform run.

### 3.2 Consequences we now own

1. **We are reselling Azure.** Customer spend lands on our bill monthly in arrears. This
   is cash-flow risk plus a margin decision, and it forces the commercial model toward
   fixed-price tiers with fair-use caps. Those tiers are only priceable because archetypes
   make the resource footprint predictable — standardization is now load-bearing for the
   business model, not just for operations.
2. **Hard cost caps per subscription are mandatory.** Budgets with automated action groups,
   plus Azure Policy denying SKUs above an allowlist. An agent that provisions a Premium
   tier is our money.
3. **We are a GDPR data processor.** Requires a DPA per customer, sub-processor
   disclosure, breach notification duty, and a data residency decision.
4. **Exit / handover story.** Subscription transfer plus repo handover. Needs to be
   written down and priced before the first customer asks.

### 3.3 Hosting platform — RESOLVED 2026-09-20

> **Superseded by D-13: Azure is the v1 platform.** Residency, not sovereignty. The analysis
> below is kept as the rationale record — it is why the alternatives were considered and what
> the decision costs us, which is worth having when the question reopens. It is no longer a
> live evaluation, and D-6 and D-7 are no longer provisional.
>
>
> **Closed.** Not to be reopened without a deliberate decision to do so — this section
> exists to record why, not to invite another round.
>
> What the decision costs: the sovereignty differentiator, until and unless a migration
> happens. That was traded for five weeks of working pipeline against an unrun spike. The
> multi-tenant model survives a platform change; only the primitives would need rebuilding.

**Residency is not sovereignty.** These are different products and they attract different
buyers:

- **Residency** — "your data sits in Frankfurt / Stockholm." Azure provides this today.
  Sufficient for a plumbing firm, a dental practice, a small logistics company.
- **Sovereignty** — "no US company can be compelled to hand it over." Requires an
  EU-owned provider. Azure cannot provide this; neither can Vercel or Supabase.

**Vercel + Supabase is ruled out.** Both are US Delaware entities. They offer EU regions
and Supabase has a proper DPA with SCCs, but both remain CLOUD Act exposed, and post
Schrems II, SCCs do not override US statute. Adopting that stack would delete our only
differentiator *and* put us in direct comparison with Lovable on their own ground.

**Candidates**

| Provider | Read |
|---|---|
| **Scaleway** (FR) | Pragmatic pick. Managed K8s (free control plane), managed PostgreSQL, serverless containers, object storage, no egress fees. **Terraform provider covers essentially the whole product surface** — decisive given our standardization model. |
| **Elastx / Safespring / Cleura** (SE) | Strongest sovereignty story, most credible for a Nordic ICP. Safespring is Swedish-owned with Swedish governance, DCs in SE and NO; Elastx runs its own DC outside Stockholm. Mostly OpenStack, fewer managed services, smaller Terraform ecosystem — more platform work on our side, which is also the moat. |
| **OVHcloud** (FR) | Most services, 40+ DCs, but Terraform requires juggling the OVH provider, the OpenStack provider and sometimes the AWS provider for S3. Directly undermines what we are building. |
| **Hetzner** (DE) | Cheapest by a wide margin, deliberately bare-bones. Good compute layer, not a platform. |
| **Azure** (current assumption) | Deepest managed services and existing in-house fluency. Residency yes, sovereignty no. If retained, use Container Apps (scale-to-zero) + PostgreSQL Flexible Server rather than App Service + Azure SQL — materially lower cost floor. |

**Trap:** Aiven is Finnish and looks sovereign, but runs its managed databases on top of
AWS/GCP/Azure. Check what is underneath before using it in a sovereignty pitch.

**What we give up by leaving Azure:** existing hands-on fluency, which is the fastest route
to a working v1. This is a real cost, not a rounding error.

**Resolution path — build, don't deliberate.** Implement the v1 golden path (container +
managed PostgreSQL + object storage, all via Terraform) on **Scaleway** first, and on one
**Nordic provider** if the sovereignty ICP looks real. A day or two each. This teaches us
more about whether their managed services can carry the archetype than any comparison
matrix. Run this **before** the agent-hosting session — where the apps run constrains
where the agents run.

---

## 4. The standardization model

Three layers, each versioned independently.

### 4.1 Infrastructure modules (Terraform)

Small, opinionated, composable modules. Not general-purpose wrappers around the Azure
provider — each module encodes our defaults and refuses most knobs.

Initial set (roles are stable; §3.3 is now resolved to Azure). Note that the POC built these
as **one composite module**, `app-stack`, rather than seven separable ones — worth revisiting
before the second archetype, since the point of separate modules is reuse across archetypes:

- `compute` — the app runtime. Azure: Container Apps. Scaleway: serverless containers or K8s
- `database` — managed PostgreSQL (preferred over Azure SQL: cheaper, and better supported
  by every code-generation model we would want to use)
- `objectstore` — S3-compatible storage, lifecycle rules, no public access
- ~~`secrets`~~ — **removed (D-14).** The goal is to manage zero secrets, not to store them
  safely. The app authenticates to Postgres with its workload identity and there is no
  credential left to put anywhere
- `observability` — logs, metrics, traces, baseline alerts
- `network` — private networking between compute and database
- `foundation` — tenancy boundary, naming, tags, access assignments

Cross-cutting rules to enforce in every module: naming convention, mandatory tags
(customer, app, environment, archetype version, cost centre), diagnostic settings on
by default, no public blob access, TLS minimums, managed identity over connection strings.

The module currently emits `owner`, `costCenter`, `managedBy`, `module`, `environment` and
`deployment` — **no version tag**, so the §5 control plane cannot report what version each
estate runs without reading every deployment's `main.tf`. See OQ-16.

### 4.2 Archetypes

An archetype = a fixed composition of modules + an application scaffold + the guardrails
on what the agent may change. This is the unit the customer effectively buys.

**Golden path (v1) — "Web app with data":**
container (web + API in one deployable) → managed PostgreSQL over private networking →
observability, in a single isolated tenancy per customer per environment. **No secret store:**
the app authenticates with its workload identity and no credential exists (D-14).

Deliberately excluded from v1: file uploads/ADLS, background jobs, custom domains beyond one,
multiple environments beyond prod + a preview slot.

**Private networking was excluded here and then built anyway** (D-15). It is in the golden
path: delegated subnets, a VNet-linked private DNS zone, and a Postgres server with no public
endpoint. It was a substantial part of the build and is not optional to reproduce.

Later archetypes (do not build yet): static marketing site; web + DB + document storage;
scheduled-job / integration app; internal form-and-report app.

### 4.3 Application scaffold

A fixed stack for the golden path so the agent generates within a narrow, testable space.
Stack chosen (D-20): **C# on .NET 10, ASP.NET Core minimal APIs, EF Core with Npgsql, xUnit v3,
one chiseled non-root container image**, with conventions fixed in the template's `AGENT.md`.
Warnings are errors, so the compiler is itself a deterministic gate (D-17). Any generated app is
recognizable at a glance.

Schema changes go through **EF Core migrations**, never ad-hoc DDL, because the agent will
change the data model repeatedly and the customer's data must survive it (D-21):

- generated by `dotnet ef migrations add`, never hand-written
- applied by the image itself (`App migrate`) in a Container Apps **init container** before each
  new replica starts; the migration tool locks its history table, so concurrent replicas apply
  each migration once, and each migration is one transaction
- **immutable once merged** and **expand-only** (no drop or rename of a table or column), both
  enforced by CI, because rollback means redeploying the previous image against the current
  schema
- CI applies every migration twice to a real Postgres before merge

The image contract is therefore: listen on `$PORT`, `/health` without the database, non-root,
and accept the single argument `migrate`.

Routes are tested **against a real Postgres** (D-24): every new or changed route gets an endpoint test
that seeds rows, calls the route and asserts exactly which rows come back, each test in its own
database cloned from a migrated template. CI fails if any endpoint test is skipped or none ran.

---

## 5. High-level architecture

Five planes. Keeping them separate is what makes the system auditable.

1. **Conversation plane** — the customer-facing chat and live preview. Turns intent into
   a structured change request. Shows cost and consequences in plain language.
2. **Agent plane** — the agents that do work. See §6.
3. **Source of truth** — one git repo per customer application, containing app code,
   Terraform, and archetype/module version pins. Every change is a commit; nothing is
   changed by hand. This is also the audit log and the rollback mechanism.
4. **Delivery plane** — CI/CD that plans and applies Terraform, runs tests, and deploys
   the app. Agents propose; the pipeline is the only thing with credentials to the
   customer's Azure. **Agents never hold customer cloud credentials.**
5. **Control plane** — our view across all customers: what version each app runs, drift,
   cost, failures, and the ability to roll a module upgrade out to everyone.

The critical invariant: *the agent's only power is to open a pull request.* Everything
that touches the customer's Azure happens in the pipeline, from committed code, with
policy checks in between.

---

## 6. Agents (sketch — full design in a later session)

Roles rather than a single do-everything agent, so each has a narrow tool surface:

- **Intake** — interviews the user, selects an archetype, produces a spec. Refuses or
  escalates anything that does not fit.
- **App developer** — writes/edits application code and migrations, runs tests, opens a PR.
- **Infra** — edits Terraform within module constraints, runs `plan`, attaches a
  plain-language summary and cost delta to the PR.
- **Reviewer** — policy and safety gate: does the diff stay inside the archetype? does the
  plan destroy data? is the estimated cost within the customer's cap?
- **Operator** — day-2: alerts, failed deploys, module upgrade rollouts, drift.

Open: where these run, how they're orchestrated, and how much human review sits in the
loop. See OQ-4 and OQ-5 — this is the topic for the next session.

---

## 7. Decisions

| # | Decision | Date | Rationale |
|---|---|---|---|
| D-1 | ~~Deploy into the customer's own Azure subscription~~ **Superseded by D-6** | 2026-08-05 | Onboarding friction was fatal for a non-technical buyer |
| D-6 | Host in our own tenancy, one isolated tenancy (Azure: subscription) per customer | 2026-08-05 | Near-zero onboarding; natural cost, quota, blast-radius and handover boundary. **Platform-specific detail provisional pending D-10** |
| D-7 | Isolation enforced by platform policy at the group level, not per-run Terraform | 2026-08-05 | Conventions rot; policy is the only durable guarantee once we host others' data |
| D-8 | ADLS / file storage excluded from the v1 golden path | 2026-08-05 | Pulls in private networking, malware scanning and lifecycle policy; not needed to prove the loop |
| D-9 | Do not compete on code generation — wrap an existing engine | 2026-08-05 | Most contested and fastest-commoditizing layer; moat is archetypes, operations and the relationship |
| D-10 | Vercel + Supabase ruled out as the hosting platform | 2026-08-05 | US entities, CLOUD Act exposed despite EU regions; deletes our differentiator and puts us head-to-head with Lovable |
| D-11 | ~~Pursue the sovereignty position; evaluate EU-owned providers via a build spike~~ **Superseded by D-13** | 2026-08-05 | Sovereignty is the one thing no hyperscaler or US PaaS can offer. Platform choice unresolved — see §3.3 and the Next Actions below |
| D-12 | PostgreSQL over Azure SQL for the golden path | 2026-08-05 | Cheaper, portable across every candidate platform, better supported by code-generation models |
| D-2 | Primary user is non-technical; no git or portal exposure | 2026-08-05 | Defines the entire UX and guardrail requirement |
| D-3 | One golden path archetype first, then expand | 2026-08-05 | Standardization only has value if the first path is genuinely finished |
| D-4 | Azure only, Terraform as the IaC language | 2026-08-05 | Stated product scope |
| D-5 | Git is the source of truth; agents only open PRs | 2026-08-05 | Audit, rollback, and credential isolation all fall out of this |
| D-13 | **Azure is the v1 platform. Residency, not sovereignty.** | 2026-09-20 | Five weeks of working Azure-specific pipeline against a spike that was never run. Supersedes D-11 and reinstates D-4. Sovereignty becomes a later positioning question, not a constraint on every proposal. The cost is stated in §3.3 |
| D-14 | Manage **zero** secrets rather than storing them safely | 2026-09-20 | Entra-only Postgres removed the last credential. Every secret Terraform manages becomes a value some identity must be permitted to read, and those permissions accumulate on the job that runs least-trusted content. Platform-independent, and the strongest result of the POC |
| D-15 | Private networking is **in** the v1 golden path | 2026-09-20 | Built and working; §4.2 had excluded it. A public database endpoint is not a defensible default once we hold other people's data |
| D-16 | ~~App scaffold: FastAPI, Python 3.12, asyncpg, pytest~~ **Superseded by D-20** | 2026-09-20 | Resolves OQ-2. Narrow enough to test, familiar enough for the generation engine. Migration tooling still absent — OQ-13 |
| D-17 | Deterministic gates, not an LLM reviewer | 2026-09-20 | JSON Schema enums, Terraform `validation` blocks, CODEOWNERS and a guardrails CI job did the §6 "Reviewer" role's work more cheaply and more reliably than a model would. Amends the §6 sketch: spend guardrail effort on constraints, not on a reviewing agent |
| D-18 | Agent runtime: Claude Agent SDK, containerised, no cloud credential in reach | 2026-09-20 | Wraps an existing engine per D-9. A command allowlist is not a boundary; credential absence is. Settles the runtime, not the hosting — OQ-4 remains open |
| D-20 | App scaffold: **C# / .NET 10**, minimal APIs, EF Core + Npgsql, xUnit v3. One language, not a default among several | 2026-09-21 | Compiler as a free deterministic gate; first-class Entra token support in Npgsql + Azure.Identity; a mature migration tool that works over the app's own tokenised connection (see D-21); code a Swedish SMB's Microsoft partner can maintain, which strengthens handover (OQ-3) and the Power Platform counter-argument. Agent support is not a differentiator either way. Cost: re-proving the image handoff with a new template. Python template retired rather than kept as an option |
| D-21 | Migrations: **EF Core, run in a `migrate` init container**, immutable once merged, expand-only, CI-enforced | 2026-09-21 | Resolves OQ-13. CI cannot reach the private database (D-15) and holds no DB rights; running in the app process would break the health contract; a Container Apps Job needs `local-exec` ordering. The init container orders migrate-before-start per revision for free, and Single revision mode keeps the old revision serving if it fails. Requires a workload-profile Consumption environment — the only kind where init containers get the managed identity. `app-stack` v0.3.0 |
| D-22 | **A successful `terraform apply` is not evidence that a deployment works.** Every apply, including the first, is followed by a readiness gate that asks the *platform* whether the new revision runs: it forces a replica, then requires `runningState = Running` and `latestReadyRevisionName` = the new revision, then `/ready` → `database: ok`. On failure the pipeline goes red, the init container's `[migrate] FAILED:` line is fetched from Log Analytics onto the deployment PR, and nothing rolls back automatically — the old revision keeps serving and recovery is a revert PR | 2026-09-23 | Amended from 2026-09-22, which had the gate assert `/ready` reports the expected migration. That cannot work: after a failed deploy the *old* revision answers, and it has nothing pending. Proven on 23 September (finding 19): red on a live-data migration failure 6 minutes after the apply finished, cause included; green on create (11s) and on the revert (37s). Fail-and-stop because an automatic rollback would change Azure outside git (D-5) |
| D-23 | **Fix-forward runs in the harness, not in the agent's turn: a fresh session per round, at most two rounds, and the harness never merges** | 2026-09-28 | Waiting for CI is not model work, so the harness polls for free and reads checks per commit. A fresh session gets the request, diff stat and trimmed log only — finding 14's "the cost is context". Proven in one round for $0.38, equal to the original push (finding 23). Merging stays with a human until OQ-5 is settled, because green is still not correct |
| D-24 | **Routes are tested against a real Postgres, one cloned database per test; the agent writes these tests but does not run them** | 2026-10-01 | Three runs (findings 21, 23, 24) passed tests that could not see a wrong route; the template's "no database in tests" decided test strength, not the brief. Now `EndpointTest` + CI's `postgres:16`, `--fail-skips on` and `--minimum-expected-tests 1` (a skipped or empty suite is red). Per test, not per class: per class failed 10 of 11 natural tests on shared rows; a clone is ~0.1s. The harness gets no Postgres - CI runs them and fix-forward (D-23) handles red, ~$0.29 a round. Six planted route bugs all went red; the agent wrote such tests unprompted (finding 25) |
| D-25 | **The change record lives in the app repo: `changes/<id>/` holds the request and its acceptance tests.** The harness writes it (brief before the builder starts, tests after its first push); CI compiles the tests into the endpoint suite and `scripts/check-changes.sh` keeps the record harness-only and immutable once merged | 2026-10-10 | Resolves OQ-23's "where". Replaces the planned private `aaas-acceptance` repo at Martin's request: the request and the behaviour it was accepted on travel with the code as its audit log, and accepted behaviour stays tested as regression. No new repo, token or CI job (D-14). Independence comes from order, not secrecy: the tests are off disk while the builder runs, and fix rounds may read but not edit them (refused by policy, by CI and by a byte check). Accepted limit: a hostile builder could rewrite history with a forged trailer. Cost: a later change cannot alter accepted behaviour without a human (OQ-24) |
| D-26 | **The harness may merge a change without a human when two independent spec-testers' tests pass and the change adds no migration, no dependency and nothing outside `src/`, `tests/` and its own record** (`change.merge_blockers`, `--auto-merge`) | 2026-10-10 | Resolves OQ-5 for one class. A pair caught 95% of planted bugs and both real defects with 0 false reds, against 90% for one run; the builder cannot touch the tests (policy, CI, byte check). A migration stays gated (OQ-7), and so does a dependency, because nothing reviews one. The check is deterministic code (D-17), not a model's judgement. Accepted risk: a rule every spec-tester run tests at one point only (finding 29) |
| D-27 | **Coding conventions live in the app's AGENT.md "Conventions", each with a CI check; requests live in the app's `requests/`, written from `requests/TEMPLATE.md`** | 2026-10-10 | Lower-level instructions belong with the app they govern, and a convention without a check is a request the agent can skip (D-17). First two: CSharpier, and Arrange/Act/Assert tests. Requests in the repo make `aaas-agent` application-free and give the requester one structure (rules, interface, examples) that both agents read |
| D-19 | **This is a build-to-learn exercise, not a business in formation** | 2026-09-20 | Stated preference: play with the technology rather than settle the commercial model. The commercial and go-to-market open questions are parked rather than deleted, so the build proceeds without them blocking and the thinking is not lost. Revisit when there is something worth selling |

---

## 8. Open questions

Ordered roughly by how much they block other decisions.

**Parked under D-19** — kept for when the commercial question becomes live again, and
deliberately not worked on in the meantime: **OQ-1a** (margin and pricing), **OQ-3** (exit
and handover terms), **OQ-3c** (ICP and whether residency is enough), **OQ-9** (commercial
model), **OQ-11** (support and SLA), **OQ-12** (compliance posture as processor). Nothing
below them is blocked by them any more; that was the point of D-19.

**OQ-12 is the one with a trigger.** It is parked only while there are no real users. The
moment anything holds someone else's data, the DPA and sub-processor questions stop being
commercial and start being legal.

- **OQ-1a — Margin and pricing on resold Azure.** Fixed tier with fair-use cap is the
  likely shape. Needs a costed footprint for the golden path before it can be priced.
  Now inseparable from OQ-9. **First real number:** one continuously running dev deployment is
  ~$17–20/month, dominated by Postgres. That is the floor per customer per environment.
- **OQ-1b — Cost caps and what happens when one is hit.** Throttle, notify, suspend?
  A suspended SMB app is a support call and a churn event.
- **OQ-1c — Subscription lifecycle at scale.** Automated subscription creation, naming,
  policy assignment, and teardown. Also: any Azure limit on subscription count per billing
  account that bites at N customers.
- **OQ-3 — Exit and handover.** Mechanism is now clear (transfer the subscription, hand
  over the repo). Open: is it contractual, what does it cost, and how long do we support
  a handed-over estate?
- **OQ-3b — Database sizing and cold starts.** **Measured:** a scale-from-zero cold start
  answered `/health` in **42.8s** on 22 September, with the init container in the path. That is
  far past "a few seconds" and well into what a visitor reads as an outage. Whatever the platform, scale-to-zero and
  auto-pause carry a 30–60s resume delay, so the first visitor each morning experiences
  what feels like an outage. Public-facing apps likely need a warm minimum during business
  hours. This sets the per-customer floor cost that OQ-1a prices against. **Note:** the POC
  runs containers at `min_replicas = 0`, but Postgres Flexible Server does not scale to zero, so
  the floor is the database and the container's cold start is the user-visible part.
- **OQ-3c — ICP: is residency enough for the buyer we are actually selling to?** Narrowed by
  D-13: this no longer blocks the platform choice, it decides go-to-market. Generic SMBs are
  satisfied by "your data stays in Sweden", which Azure provides. If the ICP turns out to be
  public sector, healthcare, defence supply chain or parts of finance, residency is not enough
  and D-13 has to be revisited — which is a rebuild, not a preference. Still the cheapest
  unanswered question on this list: ten conversations, no engineering. Still unanswered.
- **OQ-4 — Agent hosting and orchestration.** Narrowed by D-18: the runtime and tool boundary
  are settled and run locally in a container. Still open: where this runs when it is not on a
  laptop, how it is triggered by a non-technical user, and how session state and conversation
  history are held between turns.
- **OQ-5 — Human-in-the-loop boundary.** Which changes deploy automatically and which need
  a human (customer or us). Likely: app-code changes auto, schema and infra changes gated.
  **Evidence against trusting green (2026-09-26):** the agent's first feature passed CI with a
  bug its tests could not see — they asserted only that routes fail without a database.
  Auto-merge on green is only as good as the tests the agent writes, so the template and runbook
  need to make weak tests hard to write before this can lean toward "auto".
  **2026-09-28:** asked explicitly for tests through the endpoint, the agent still wrote 503 checks
  and in-memory `IQueryable` tests, because the template says tests run without a database. The
  template decides test strength, not the brief.
  **2026-09-29 (finding 24):** a third time, with a brief that asked for endpoint tests.
  **2026-10-01 (finding 25, D-24):** fixed in the template; the agent wrote exact-row endpoint tests
  unprompted. The new risk is the fix round: it corrected a wrong *test* by editing its assertion until
  CI agreed. Right that time; with the code wrong it turns a caught bug green. Before auto-merge, either
  a fix round may not weaken an existing assertion (a deterministic diff check, D-17), or correctness is
  checked by tests the agent never sees. The next session measures the latter.
  **2026-10-04 (finding 26) - position: not on the agent's CI alone.** 1 of 3 briefs, the largest, went green
  with a stated rule broken, caught only by tests written from the spec by someone else. Auto-merge needs
  those as a required check (OQ-23). The fix-round assertion guard is still unsized: 0 edits in 1 round.
  **2026-10-10 (finding 28) - position: not yet, and the reason is the spec-tester's run-to-run variance.** The gate
  now exists (D-25) and fix rounds cannot edit it. But one of three spec-tester runs on the same brief passed the
  defect the other two caught. Next: two independent spec-testers per change, tests combined; if the union holds,
  the harness merges route-only changes with no migration.
  **2026-10-10, evening (finding 29, D-26) - resolved for one class.** Two spec-testers; the harness merged #22 with
  no human. Everything with a migration or a dependency still waits for one. The open part is narrower: the shared
  blind spot, and how a harness merge is told apart from a human one in the audit trail.
- **OQ-24 — Superseding accepted behaviour.** Since D-25, every merged change's acceptance tests keep running and
  its record is immutable. A later request that deliberately changes that behaviour turns an old test red, and
  neither the builder nor CI may edit it. Today that is an admin merge by a human. Open: does the spec-tester of
  the new change propose the replacement, does the old test get retired by a harness commit naming the new change,
  and who approves? Same shape as OQ-21 (an immutability rule that needs a governed exception).
- **OQ-6 — Cost control.** Per-customer budget caps, what happens at the cap, how cost is
  estimated *before* apply and shown to a non-technical user.
- **OQ-7 — Data model evolution.** Mechanism settled by D-21 (expand-only, immutable,
  applied before start). Still open: who performs and approves the second half of a
  two-release removal, and whether preview environments with seeded data are needed to test
  a migration against realistic data before it meets the customer's.
- **OQ-8 — Module upgrade rollout.** How a new module version reaches N customer estates
  without a breaking change taking down all of them at once.
- **OQ-9 — Commercial model.** Subscription per app, per archetype, usage-based, or
  setup + retainer. Interacts with D-1 since Azure spend is on the customer's bill.
- **OQ-10 — Secrets and third-party integrations.** SMBs will want email, payments, and
  their existing SaaS. Every integration is a new credential and a new failure mode.
- **OQ-11 — Support and SLA.** A non-technical user with a broken app will call someone.
- **OQ-18 — Agent inference as COGS.** The cost model prices infrastructure and nothing
  else. Anthropic licenses subscription OAuth for individual use only, so the POC can run on
  a personal subscription but the product cannot: from the first customer, the agent plane
  needs API billing. That is a per-customer variable cost that scales with how much the
  customer talks to the product — unlike the infrastructure floor, which is fixed and
  predictable. Fix-forward loops and chatty intake sessions are the expensive cases. Needs a
  measured number before the OQ-1a tiers are set, and the first harness run is what produces
  it (the run record reports cost per run). **First numbers (2026-09-26):** $0.65 for a
  deployment PR, $0.90 for a new feature, $0.43 for a bug fix, all single-shot. Fix-forward
  rounds are unmeasured and are where the multiplier lives.
  **27 September (finding 22):** an afternoon of iterating on one side-quest app cost ~$13 across six
  runs, 29% of it lost to two runs that hit a cap (`max_turns`, then the personal plan's 5-hour limit)
  and pushed nothing. A subscription is a capacity ceiling as well as a licence problem.
  **28 September (finding 23):** one fix-forward round cost $0.38, the same as the push it fixed.
  A red check roughly doubles the agent cost of a change; that is the multiplier to price.
- **OQ-21 — "Immutable once merged" is the wrong rule.** CI forbids editing a migration once it
  is on `master`, but the migration that failed in Azure was merged and never applied. Fixing it
  forward is therefore blocked by the guard, and recovery needs a human override — at exactly
  the moment an application is broken. The rule should be "immutable once **applied**", which
  requires the pipeline to know what each estate has actually applied. That is the same missing
  fact as OQ-16 (version visibility), reached from the other direction.
- **OQ-22 — What a non-technical user is told when a deploy fails.** The pipeline half is done
  (D-22): the deployment PR carries the URL on success, and on failure the reason and the
  init container's own error line. Still open: the §2 persona reads neither a PR nor
  `23505: could not create unique index`. Someone has to translate "your two items called
  X share a name" and decide who opens the revert — us, automatically, or the customer by
  pressing something. Interacts with OQ-5.
- **OQ-14 — Operations that fail unrecoverably.** **Update, 2026-10-01 (finding 25):** the v0.3.2
  ordering fix is proven by a clean create. **Update, 2026-09-29 (finding 24):** the create
  failure of 23 recurred and its cause was found: the Container App's `migrate` init container created
  the database itself (EF Core creates a missing one; the app's identity is administrator) one second
  before Terraform tried to. Fixed by ordering (`app-stack` v0.3.2). The general form: *anything the
  platform starts during an apply can create what Terraform expects to create* - the app's startup is
  part of the create path while it holds rights to create things. **Update, 2026-09-28 (finding 23):** a *create*
  failed after 9 minutes on `appdb` "already exists" in Azure but not in state. Recovery was an
  `import` block in a PR, then a second PR to remove it: minutes for us, impossible for the §2 user.
  Fourth Postgres-child failure; the first on create. **Update, 2026-09-24 (finding 20):** the teardown
  hang is sidestepped - `destroy.yml` drops the Postgres child resources from state and deletes
  the server directly - but the evening that took exposed the general form of this question: a
  destroy reported success in 18 seconds while the entire stack still existed, because
  Terraform's state and Azure disagreed. Every pipeline operation needs a check against Azure
  afterwards, not just Terraform's report (the readiness gate is that check for apply). **Second instance, 2026-09-22 (finding 18):**
  teardown hung for 30+ minutes on three Postgres child resources and then failed reporting an
  OIDC renewal error, twice in a row. Recovery was a manual `az group delete` plus a third run to
  empty the state. Two distinct failures now share a shape — the pipeline reports the layer that
  noticed, not the layer that broke — and neither had an automatic recovery path. A cancelled workflow left the Terraform state
  lease held, and recovery required a manual `az storage blob lease break` against the state
  account. For the §2 persona that is not a recovery step — the equivalent trigger is closing a
  browser tab. A `unlock.yml` workflow exists for this one case; the general question is which
  other operations have no automatic recovery path (partially applied deploy, image built but no
  PR opened, apply cancelled mid-flight) and what the customer-facing answer is for each.
- **OQ-15 — Automated provisioning *and verification* of per-repo identity.** Onboarding one
  application currently needs a repo, a GitHub App installation, two repository secrets, a
  deployment directory and a federated credential per environment. Every one of those failed at
  least once during the POC, none of them failed legibly, and the errors never named the cause.
  This list *is* the productisation surface. Whatever automates it must also assert afterwards
  that the presented identity matches the registered one, rather than waiting for a deploy to
  fail eight minutes in.
- **OQ-16 — Version visibility across the estate.** Resources carry no archetype or module
  version tag, so §5's control plane cannot answer "what is each customer running" from cloud
  metadata. Blocks OQ-8 (rollout) in practice: you cannot stage an upgrade across estates you
  cannot enumerate.
- **OQ-17 — GitHub plan cost.** Branch protection and rulesets are unavailable on private repos
  on the Free plan; the POC repos were made public to get real enforcement. Customer repos are
  necessarily private, so enforcement requires a paid plan or an organisation. A per-customer
  cost line and a decision, not a detail.
- **OQ-12 — Compliance posture as data processor.** DPA template, sub-processor list,
  breach notification process, data residency (single region vs. per-customer choice),
  and whether any archetype will ever touch personal or regulated data (it will).
  Materially heavier now that we host, per D-6.

---

## 9. Roadmap

The ordered plan the build follows. `NEXT-SESSION.md` holds one step of it and names which line it
serves; if the two disagree, one of them is wrong and gets fixed at session end. Rewritten
2026-10-04 (the previous §9 still said "spike Scaleway first" five weeks after D-13).

**Where the planes stand (§5).** Source of truth: done, for an app that already exists. Delivery:
done and hardened (plan, apply, readiness gate D-22, clean create, destroy). Agent: the app-developer
role works with fix-forward (D-23); intake, infra-as-a-role and operator do not exist and hosting is
open (OQ-4). Conversation plane: nothing. Control plane: nothing, and blocked in practice by OQ-16.

**Everything since 23 September** closed one reason green did not mean works (D-22, Phase 4c, D-23,
OQ-14, D-24). They are one question - can a merge happen without a human (OQ-5) - which is the hinge
of the product: the §2 user cannot review a PR.

| # | Step | Serves | Done when |
|---|---|---|---|
| 1 | ~~**Phase 7: is green correct?**~~ Done 2026-10-04 (finding 26): 2 of 3 correct; the largest green-but-wrong on an untested stated boundary | OQ-5 | ✓ |
| 2a | ~~**A spec-tester role.**~~ Done 2026-10-04 (finding 27): no false reds, #16 caught, 15/17 planted bugs | OQ-23 | ✓ |
| 2b | ~~**Act on it.**~~ Done 2026-10-10 (finding 28, D-25): the change record; hidden tests are part of the required check. No auto-merge: one spec-tester run in three missed a known defect | OQ-5, OQ-23 | ✓ (reason recorded) |
| 2c | ~~**Two spec-testers per change**~~ Done 2026-10-10 (finding 29, D-26): pair 95% vs single 90%; #22 merged by the harness | OQ-5 | ✓ |
| 3 | **A new app from nothing** - repo, ruleset, identity, federated credentials, deployment directory, verified after creation | OQ-15 | One command or workflow takes a name to a running empty app, and asserts its identity before the first deploy |
| 4 | **Version visibility** - module/archetype version tags on resources, and what each estate has applied | OQ-16, OQ-21 | "What is each app running" is answered from Azure metadata; the migration guard becomes "immutable once applied" |
| 5 | **The first missing plane** - intake + conversation (OQ-4, OQ-22) or control plane (OQ-8). Chosen by interest when step 4 is done (D-19) | §5 | One end-to-end path through that plane, however thin |

Off the path, picked up only if they block a step: the `workload_profile_name` diff, a ruleset on
`aaas-deployments`, `.terraform.lock.hcl`, the policy's argument-as-command false positives.
Parked under D-19: everything commercial, including OQ-3c.

## 10. Working conventions for this document

- One section per stable concept; volatile thinking lives in Open Questions.
- When an open question is resolved, add a row to §7 with the rationale and delete the
  OQ entry — do not leave both.
- Keep the decision rationale, not just the decision. Future-us will want to know why.
- Bump the version and date in the header on every substantive edit.
