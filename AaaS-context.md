# AaaS — Application as a Service

**Status:** v1 platform decided · POC pipeline working · agent produces infrastructure PRs · .NET scaffold with migrations proven in Azure  
**Owner:** Martin Ingeson · **Last updated:** 2026-09-22 (v0.7)

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
update.

**Not proven.** The agent writing an application. It produces infrastructure PRs (Phase 4a,
4m 11s prompt-to-PR); application code generation has not been run, so prompt-to-running-app
time is still unknown.

**This document drifted from the build.** The POC documents do not reference it, which is how
five weeks of Azure-specific work happened while §9 still said "spike Scaleway first".
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
| D-22 | **A successful `terraform apply` is not evidence that a deployment works.** Deploys need a post-apply readiness gate | 2026-09-22 | Proven, not theorised: a migration that fails only against live data produced a green apply, a revision in `ActivationFailed` holding 100% of the traffic, and an app that kept serving the old version. At `min_replicas = 0` nothing starts at apply time, so nothing fails. The gate must force a replica, wait, and assert `/ready` reports the expected migration. Not yet built — it is the next piece of work (finding 16) |
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
  it (the run record reports cost per run).
- **OQ-21 — "Immutable once merged" is the wrong rule.** CI forbids editing a migration once it
  is on `master`, but the migration that failed in Azure was merged and never applied. Fixing it
  forward is therefore blocked by the guard, and recovery needs a human override — at exactly
  the moment an application is broken. The rule should be "immutable once **applied**", which
  requires the pipeline to know what each estate has actually applied. That is the same missing
  fact as OQ-16 (version visibility), reached from the other direction.
- **OQ-22 — What the deploy reports, and to whom.** `app_url` is produced by the apply and
  discarded; a failed init container is visible only by querying Log Analytics directly, because
  `az containerapp logs show --container migrate` cannot find it; and the traffic weight shows
  100% pointed at a revision that never ran. The readiness gate (D-22) has to fetch the URL and
  read the init container's log anyway, so this is one piece of work, not three. What a
  non-technical user is told when it fails is the open part.
- **OQ-14 — Operations that fail unrecoverably.** **Second instance, 2026-09-22 (finding 18):**
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

## 9. Next actions

> **This section is stale and needs rewriting.** Items 1 and 2 are superseded by D-13; the
> rest was written before the POC existed. The current next step is in `STATUS.md`: run the
> agent harness once, end to end, and time it.

1. ~~**Golden path spike on Scaleway.**~~ Superseded by D-13.
2. ~~**Same spike on one Nordic provider.**~~ Superseded by D-13.
3. **Resolve OQ-3c** by talking to 5–10 SMBs in the target segment. Ask what they would
   pay for "data stays in Sweden, Swedish company" versus not caring. This is cheaper than
   any of the engineering above and it invalidates or confirms the whole positioning.
4. **Then** the agent hosting session (OQ-4). Where apps run constrains where agents run.

## 10. Working conventions for this document

- One section per stable concept; volatile thinking lives in Open Questions.
- When an open question is resolved, add a row to §7 with the rationale and delete the
  OQ entry — do not leave both.
- Keep the decision rationale, not just the decision. Future-us will want to know why.
- Bump the version and date in the header on every substantive edit.
