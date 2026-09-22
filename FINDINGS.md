# AaaS POC — findings log

Running record of what the build actually taught us, kept separate from the plan so it stays honest. Started at first apply, 31 August 2026.

---

## 1. The image handoff worked first time

The cross-repo chain — app merge → image build → immutable SHA tag → automated PR against the deployments repo — succeeded on its first genuine attempt, and produced exactly the intended one-line diff (`container_image` only).

This was flagged in the plan as the most failure-prone link and the reason to build Phase 4b before any code generation. That judgement was wrong in a useful direction: the mechanism was fine, and the real fragility was somewhere else entirely (see finding 2). Worth remembering when estimating the next unfamiliar piece — the thing that *looks* intricate is not reliably the thing that breaks.

**Implication:** the tfvars-as-data design held under a real automated writer. A machine-generated deploy is a reviewable one-line diff, which is what makes the "show a non-technical user what this change costs" idea viable later.

---

## 2. The OIDC subject was wrong three times, for three unrelated reasons

Every failure produced the same message: `AADSTS700213: No matching federated identity record found`. It reads as a permissions problem and never once was.

| # | Cause | Why it wasn't obvious |
|---|---|---|
| 1 | **Immutable subject claims.** Repos created after 15 July 2026 embed owner and repo IDs in the `sub` claim (`repo:owner@40392502/repo@1328777566:...`) and cannot opt out. | Every guide written before mid-2026, including this one initially, produces the old format. |
| 2 | **Default branch was `master`, not `main`.** | Cosmetic-looking; the subject is matched by Entra as an exact string. |
| 3 | **`environment:` replaces the branch ref.** A job declaring `environment: dev` presents `:environment:dev`, *not* `:ref:refs/heads/master`. | The workflow still only runs on the default branch, so a branch-scoped credential looks correct. |

**Implication — the most important one so far.** In the eventual product every customer gets their own repo and their own identity, and this setup step must be *generated and verified programmatically*. A human following a runbook will get it wrong, and the error will not tell them why. Any onboarding flow needs a post-setup verification that asserts the presented subject matches the registered credential, rather than waiting for a deploy to fail.

---

## 3. Per-repo secret provisioning is the dominant setup cost

Three separate failures, all the same shape: a secret missing or malformed on one repo but present on another.

- `AAAS_APP_PRIVATE_KEY` unset on `aaas-deployments`, then again on `aaas-app-demo`
- `AAAS_APP_ID` set to the **Client ID** (`Iv23li…`) instead of the numeric App ID

None surfaced as a configuration error. They surfaced as a Node stack trace inside a third-party action, 40 seconds into a job, naming the input but not the repository or the likely mistake.

Mitigated with a preflight step that validates shape and names the repo. That is a patch, not a fix.

**Implication:** onboarding a new application repo currently requires repo creation, GitHub App installation, two secrets, a deployment directory, and an OIDC credential per environment. That list *is* the productisation surface. Every item is a place a customer-facing flow must automate and then verify.

---

## 4. Scaffolding failed more than the infrastructure

Rough tally to first successful apply:

- **~6 failures in CI scaffolding and identity config** — lint config, path filters, OIDC subjects, secrets
- **2 genuine Terraform issues** — a deprecated Key Vault argument, and provider auto-registration failing under a read-only identity

The Terraform module was close to right on the first attempt. The machinery around it was not. Worth weighing when judging whether the underlying design is sound: the design has not yet been contradicted, but the operational surface is larger than the design.

---

## 5. The read/write identity split worked as intended

The `plan` job failed with 403s trying to register Azure resource providers — because it runs as `Reader` and genuinely cannot. That is the least-privilege boundary doing its job, discovered the correct way (a failed plan, not a surprise write).

Fixed properly, by having `bootstrap.sh` register the specific providers the module needs and setting `resource_provider_registrations = "none"`, rather than by widening the plan identity. Keeping that discipline under time pressure is the whole point of having the split.

---

## 6. Guardrails behaved correctly, including inconveniently

The `guardrails` job failed a PR that modified `.github/` — that PR being the one installing the guardrails job itself. Correct behaviour, mildly annoying, and exactly what should happen when an agent tries to widen its own constraints.

Also confirmed: **GitHub Free does not support branch protection or rulesets on private repositories.** The repos were made public to obtain real enforcement. For a product where customer repos are necessarily private, this is a cost line, not a detail — enforcement requires a paid plan or an organisation.

---

## 7. Cost tracked to plan

Estimate before starting was ~$17–20/month for one continuously running deployment, dominated by Postgres. Nothing so far contradicts it; spend before the first apply was effectively zero. Budget alert set at $20 with a forecast notification.

SKU and storage enums were tightened before bootstrap so no single deployment can get expensive. The realistic overspend remains deployments left running, not anything mis-sized.

---

## 8. First real apply failed after 8 minutes — a half-configured credential

Run `33422816041`, 31 Aug 2026. All infrastructure built successfully — VNet, subnets, private DNS, **Postgres Flexible Server**, Key Vault, identity, Log Analytics, Container App environment. It failed on the very last resource:

```
ContainerAppSecretInvalid: Container app secret(s) with name(s)
'registry-password' are invalid: value or keyVaultUrl and identity
should be provided.
```

`GHCR_READ_TOKEN` was never set, so `TF_VAR_registry_password` was empty. The module created a `registry-password` secret anyway, because the `dynamic` blocks were gated on `registry_username` rather than on the password. Azure rejects an empty secret value.

**Notably, the suspected cause was wrong.** The prediction was that Postgres delegated-subnet and private-DNS ordering would be the failure — the intricate part. That part worked on the first attempt. The failure was a one-line boolean in a conditional. Second time in this exercise the intricate thing worked and something trivial didn't (see finding 1).

**Fixed at the right level.** The condition now requires both username and password, and two `precondition` blocks reject a half-configured credential at **plan time** with a message naming the actual remedy. Same class of fix as the `preflight` step in finding 3: the bug was cheap, the eight-minute unactionable feedback was the expensive part.

**Design point this reinforces:** a missing *deployment-level* credential should be catchable before anything is provisioned. The tfvars schema cannot see it, because the value arrives from CI as an environment variable rather than from the tfvars file. Anything supplied that way sits outside the validation layer that makes this design safe. Worth reviewing whether other CI-injected values have the same blind spot.

**Datapoint:** module feedback loop is ~8–15 minutes, dominated by Postgres. Slow enough to shape how Phase 1 debugging is done — iterate against a long-lived resource group rather than full create/destroy cycles, and push every check that can be a precondition to plan time.

---

## 9. The application worked — first genuine success

After fixing the registry credential and making the GHCR package public, the full stack applied and both endpoints responded. `/ready` returned `{"database":"ok"}`, confirming the delegated subnet, private DNS zone and connection-string path all work. The APIs were exercised successfully.

**Phase 1 is proven.** The module builds a working private-networked application.

---

## 10. A redeploy was NOT a one-line diff — two module bugs

Testing the *update* path (the second half of Phase 4b) exposed what a create-only test could not. The plan for an image bump read:

```
Plan: 1 to add, 1 to change, 1 to destroy
```

Neither the add nor the destroy had anything to do with the image.

**Bug A — the module's desired state depended on who ran Terraform.**

```
~ principal_id = "335ee422-..." -> "0c59689f-..." # forces replacement
```

The Key Vault deployer role assignment used `data.azurerm_client_config.current.object_id`. That resolves to the *apply* identity during apply and the *plan* identity during plan, so every plan proposed destroying and recreating it. A module whose desired state varies with the caller is broken, and this is invisible until you have two identities — which is exactly what the read/write split introduced.

**Bug B — perpetual diff on the Container App environment.** Azure attaches a default `Consumption` workload profile whether or not one is declared. Not declaring it meant every plan showed an in-place update removing it.

**Bug C — the 403 that stopped the plan.** `azurerm_key_vault_secret` requires a data-plane read on refresh, which the Reader-only plan identity cannot do.

### Resolution: Key Vault removed

All three fixed by dropping Key Vault and passing the connection string directly as a Container App secret (write-only through the API, no refresh read), plus declaring the workload profile explicitly.

The Key Vault removal is worth stating plainly, because it reversed a decision that looked obviously correct: **Terraform constructs the connection string, so the value is in state either way.** Key Vault was guarding a secret already written to the state blob, while costing a propagation sleep, two role assignments, a caller-dependent role assignment, and a soft-deleting vault that complicated teardown. It was security theatre with real operational cost.

For the product, secrets should be rotatable independently of Terraform — but the answer is to take the value *out* of Terraform, not to put a vault in front of a value Terraform already knows.

### The methodological point

**Testing create is not testing update.** All three bugs were invisible on a green first apply and would have surfaced on a customer's second deploy. Any future acceptance criterion should require a *second* deploy showing a one-line diff, not just a successful first one.

---

## 11. A read-only plan identity is incompatible with Terraform managing secrets

Fixing finding 10 by moving the connection string from Key Vault into a Container App secret did not fix the underlying problem. The next plan failed with:

```
does not have authorization to perform action
'Microsoft.App/containerApps/listSecrets/action'
```

Same failure, different resource. The azurerm provider reads secret values back on refresh, whether they live in Key Vault (`getSecret`) or in a Container App (`listSecrets`). A Reader identity can do neither.

**The general rule: if Terraform manages a resource holding a secret, the identity that runs `plan` must be able to read that secret.** Options are all unattractive:

| Option | Cost |
|---|---|
| Grant plan identity secret-read | Plan runs Terraform against unreviewed PR content, so a PR could add an output that exfiltrates it |
| `plan -refresh=false` | Works, but hides drift |
| Drop the read/write split | Loses the boundary that already caught two real problems |
| Remove the secret entirely | Requires rethinking authentication |

### Correction: removing the secret did NOT fix this

The reasoning above was wrong, and the fix based on it failed. The azurerm provider calls `Microsoft.App/containerApps/listSecrets` on **every** Container App read — unconditionally, whether or not the resource has any secrets ([provider issue #31181](https://github.com/hashicorp/terraform-provider-azurerm/issues/31181)). The `read_secrets = false` escape hatch exists only on the *data source*, not the managed resource.

So a Reader identity cannot refresh a Container App at all. Not "a Container App with secrets" — any Container App.

**The actual resolution: `terraform plan -refresh=false` on PRs.** Plan compares config against state rather than against Azure, needs no permissions beyond Reader, and exposes nothing. `apply` still refreshes normally under Contributor. The PR comment now carries a caveat saying drift is not detected. If Terraform stops being the only writer, the answer is a scheduled drift-detection workflow under the apply identity — not weakening the plan job.

**Process lesson, and the sharper one.** This was diagnosed three times and fixed wrongly twice, both times because a plausible mechanism was assumed rather than checked. The provider's behaviour was documented in a public issue the whole time; two apply cycles and a module rewrite would have been avoided by five minutes of reading. The tell was available after the *second* failure: the same error on a different resource should have prompted "what is actually making this call?" rather than another attempt to move the secret.

The Entra-only auth work below is still worth keeping on its own merits — no credential in state, automatic rotation, no Key Vault — but it should be recorded as a good change that did not solve the problem it was made for.

### Resolution of the credential question: there is now no database password at all

Postgres is configured for **Entra ID authentication only** (`password_auth_enabled = false`), the app's managed identity is the database administrator, and the app fetches a token at connect time. Removed as a consequence: `random_password`, the admin login, Key Vault, two role assignments, the propagation sleep, and the Container App secret.

Nothing secret remains for Terraform to manage, so plan works under Reader with a full refresh and no new permissions. Credentials also rotate hourly by themselves.

**This took three attempts, and the first two were the same mistake in different clothes.** Moving a secret does not remove the requirement to read it. Only deleting the secret did. Worth remembering as a general reflex: when a permission problem recurs after a fix, check whether the fix moved the thing or removed it.

### For the product

This is the strongest architectural result of the POC so far, and it generalises: **an agentic deployment pipeline should aim to manage zero secrets.** Every credential Terraform stores becomes a value some identity must be allowed to read, and those permissions accumulate on exactly the job that runs least-trusted content. Managed identity everywhere is not just tidier — it is what keeps the least-privilege boundary viable.

### Also confirmed here

Moving the git tag `v0.1.2` rather than cutting a new one produced a plan that disagreed with the merged module — Key Vault resources appearing as "to create" against a state that no longer had them. **Cut a new tag per change; never move one.** Terraform resolves a ref at init and gives no indication the tag has moved beneath it.

---

## 12. Phases 1, 2 and 4b proven

```
GET /ready -> {"database": "ok", "auth": "managed-identity"}
```

Confirmed working end to end:

- **Phase 1** — the module builds a private-networked application. Postgres has no public endpoint, resolves over a private DNS zone from a delegated subnet, and the app reaches it.
- **Phase 2** — PR → schema validation → plan → merge → apply, with split read/write identities and no stored Azure credential.
- **Phase 4b** — the cross-repo image handoff works for **both** create and update: app merge → image build → automated deployment PR → applied revision.
- **Passwordless database authentication** — no credential exists in Terraform state, Key Vault, a Container App secret, or the repo. Tokens rotate hourly on their own.

The last of those was not in the original plan. It came out of chasing a permissions failure and turned into the most transferable result of the exercise.

### What it took

Roughly 25 distinct failures to get here. The breakdown matters more than the count:

| Category | Count | Notes |
|---|---|---|
| Identity and credential configuration | ~10 | OIDC subjects, GitHub App secrets, package visibility |
| CI scaffolding | ~6 | lint config, path filters, workflow triggers |
| Terraform module logic | ~5 | registry gating, caller-dependent role assignment, workload profile |
| Provider behaviour we assumed rather than checked | ~3 | `listSecrets`, immutable claims, environment subjects |
| Sequencing and stale state | ~2 | moved tags, stale branches |

**The infrastructure design was rarely the problem.** What consumed the time was identity plumbing and unverified assumptions about how tools behave — both of which are exactly the parts a product would have to automate and verify on behalf of a non-technical user.

---

## 13. A cancelled run leaves state permanently locked, with no way out

Cancelling a running `destroy` left the azurerm backend's blob lease held. Every subsequent plan, apply and destroy then failed with `state blob is already locked`, and **nothing in the pipeline could recover it** — the fix required a manual `az storage blob lease break` against the state account.

The trigger was trivial: a workflow cancelled to run a `curl`. In the product, the equivalent is a user closing a browser tab, and it would leave their deployment permanently wedged with an error mentioning a blob lease.

**Added `unlock.yml`** — manual dispatch, requires typing `UNLOCK`, and refuses to run if another plan/apply/destroy is in progress. That guard is deliberately conservative: breaking a lock on a *live* apply can corrupt state, which is much worse than a stuck one.

### The general point

This is a class of problem the POC would not have surfaced through happy-path testing: **an operation that fails in a way the system cannot recover from by itself.** Worth auditing for others before anything runs unattended — a partially-applied deployment, an image that builds but never gets a PR, a merged bump whose apply is cancelled. Each needs either an automatic recovery path or a clearly-labelled manual one.

For a product whose premise is that non-technical users deploy applications, "you must run an Azure CLI command against the state account" is not an acceptable recovery step.

---

---

## 14. The agent works — and the cost has a shape worth knowing

Run `20260920T133807Z`, 20 September 2026. One natural-language request in, one deployment PR out, `Plan: 14 to add, 0 to change, 0 to destroy`, no human edit to the tfvars. **Phase 4a is proven.**

| | |
|---|---|
| Wall clock | 4m 11s |
| Exchanges / model turns | 2 / 24 |
| Tool calls | 13 `Bash`, 6 `Read`, 3 `Write` |
| Cost | $0.65 (shadow — see below) |
| Policy refusals | 2 |
| Outcome | PR #7, plan green on first attempt |

The infrastructure choices were correct without being spelled out: `B_Standard_B1ms`, `min_replicas = 0`, `cpu 0.25 / memory 0.5Gi` for *"about fifteen people, only during working hours, nobody minds if the first page load takes a few seconds"*. That is the sizing table in `create-deployment.md` being applied as intended — which is an argument for writing runbook guidance as a table of situations rather than as prose about tradeoffs.

### The cost is context, not output

Nothing was billed: the run used a Claude subscription token, and `total_cost_usd` is what the equivalent API usage *would* have cost. That shadow figure is the number the product would actually pay (OQ-18), so it is worth reading properly rather than noting as "under a dollar".

| Component | Exchange 1 | Share |
|---|---|---|
| Cache reads (463k tokens) | $0.139 | 44% |
| Cache writes (29k tokens) | $0.110 | 35% |
| Output (4.4k tokens) | $0.066 | 21% |
| Fresh input (24 tokens) | $0.0001 | 0% |

Reconstructing this from standard Sonnet rates reproduces the reported figure to the cent, so the shape is real: **79% of the cost is moving context in and out, and 21% is the model producing anything.** The agent read the schema, the module README and the runbook once, then carried all of it through 23 model turns.

**The sharper result: exchange 2 cost more than exchange 1.** It was a single turn that produced a refusal and changed nothing — $0.335, against $0.314 for the 23 turns that did all the work. The context had grown, and one pass over a 492k-token context is simply what a follow-up question costs.

**Implication, and it is a product-level one.** Cost scales with context size times number of turns, not with how much gets built. The expensive customer is not the one who asks for a complicated application; it is the one who asks six follow-up questions, because each one pays for the whole conversation again. A conversational front end for non-technical users — exactly what §2 of the product context specifies — is the most expensive possible shape, and fix-forward loops multiply it. Pricing tiers built on "cost scales with the app" would be wrong in a way that only surfaces once the chattiest customers arrive.

Two cheap mitigations, neither tried yet: keep the runbook and schema out of the carried context once the tfvars is written, and start a fresh session per task rather than continuing a conversation.

### The refusals were useful, and one of them was our fault

Both refusals were the same rule — command substitution is not allowed, because it hides the command actually being run from the check that decides whether to allow it.

| Attempt | Verdict |
|---|---|
| `GH_TOKEN=$(gh auth token) git push …` | Agent solving an auth problem it did not have. It recovered by itself and the push succeeded. |
| `gh pr create --body "$(cat <<'EOF' …)"` | **Our bug.** `create-deployment.md` said `--body "…"`, so the agent reached for the idiomatic multi-line form and hit the wall. |

The runbook now says `--body-file /tmp/pr-body.md` and explains why, matching what `release.yml` already does for the same reason.

**This is the feedback loop the harness was built for, working on its first run.** The report's own text predicts it — *"a refusal that recurs is usually a runbook problem, not an agent problem"* — and one of two refusals was exactly that. Recording refusals is cheap and it is the mechanism by which the runbooks get better without anyone guessing.

### The module ceiling was reached on request one

POC-PLAN §11 asks how often a request needs something the module cannot express. On the first real request the answer was "immediately" — though for an atypical reason: the requester wanted a faster test loop, not a different application. Asked to drop the database to avoid a 15-minute provision, the agent refused, and the refusal is worth reading:

- it named the binding constraint exactly — the module always provisions Postgres, no flag exists, and `additionalProperties: false` means one cannot be invented
- it showed the deployment was already at the module's floor, with the numbers
- it correctly identified that the fix belonged to whoever owns the module, not to itself
- it offered to close the PR

That is the behaviour the escape hatch needs, produced from `PROMPT.md` alone with no special handling. It does not answer the product question — what happens when a paying customer wants something the archetype cannot express — but it establishes that the refusal path degrades into a clear explanation rather than into approximation. An agent that had "helpfully" removed the database, or edited the schema to allow it, would have been the failure mode worth fearing.

### Getting it to run cost five scaffolding failures

Consistent with finding 4, and worth tallying because the ratio keeps holding:

| Failure | Cause |
|---|---|
| `IMAGE?: unbound variable` | A Unicode ellipsis pressed against a variable in `run.sh`; macOS bash 3.2 reads it as part of the name |
| `FileNotFoundError: briefs/…` | The brief lived on the host; only `runs/` was mounted |
| `--dangerously-skip-permissions cannot be used with root` | The image ran as root deliberately, to dodge a hypothetical bind-mount papercut |
| `Permission denied` on the entrypoint | `COPY` preserved 0600, `chmod +x` gave 0711 — execute without read, fatal for a script |
| Edits appearing not to take effect | `harness/` is baked into the image; no rebuild was triggered |

Zero of these were the policy engine, the runbooks or the SDK integration. All five were legible within seconds because they happened in a container on a laptop rather than eight minutes into an Azure apply — which is the argument for the container restated as evidence.

The file-mode one deserves a note of its own: it existed **only in one working copy**, because git records just the executable bit and a fresh clone would have had normal modes. A bug reproducible from one machine's filesystem state and not from the artefact is the worst shape available, and the fix belongs in the Dockerfile — set modes explicitly, never inherit them.

---

## 15. The scaffold is C#/.NET now, and the compiler pays for itself

D-20. The Python scaffold was replaced by ASP.NET Core minimal APIs on .NET 10, EF Core with
Npgsql, xUnit v3, in a chiseled non-root image (193MB). Agent support was not the deciding
factor and is not a differentiator either way; the reasons that held up were: the compiler is a
free deterministic gate (D-17) for a class of mistakes Python only surfaces at runtime, Npgsql +
Azure.Identity support the Entra token natively, EF Core gave a mature migration tool that works
over the app's own tokenised connection, and the output is code a Swedish SMB's Microsoft
partner can maintain (OQ-3, and the Power Platform counter-argument).

Four bugs were found by running the template's own checks before anything reached Azure, and
the first is the one worth remembering:

| Found by | Bug |
|---|---|
| A test | An invalid request returned **503 instead of 400** with no database configured. Minimal APIs resolve `AppDbContext` during parameter binding, *before* validation runs, so a context that threw on construction turned every validation error into a database error. Fixed by making the context always constructible and failing at connection-open instead |
| Build | Npgsql pulled EF Core 10.0.4 alongside 10.0.12. Only a warning — and `TreatWarningsAsErrors` does not cover MSBuild warnings, which is its own trap |
| Running the image | EF logged `fail:` lines on a fresh database and whenever replicas raced, which in an init container log reads like the cause of whatever failed next |
| Running the image | Npgsql probes for Kerberos (GSSAPI); the chiseled image has no Kerberos library |

**A local `dotnet` loop is not enough on its own.** `dotnet ef` spells the build configuration
`--configuration`, because its `-c` means `--context` — a mistake that only shows up when the
command actually runs.

## 16. A green deploy proves nothing at `min_replicas = 0`

This answers OQ-19, and the answer is the bad one. The test: two items sharing a title, then a
migration adding a unique index on that column. It passes CI — CI's database is empty — and can
only fail in Azure, which is the realistic shape of a production migration failure.

Sequence, 22 September:

| | |
|---|---|
| `terraform apply` | **success**, 18:53:26. Nothing in the pipeline said otherwise |
| New revision `--0000002` | `ActivationFailed`, 1 replica, **100% traffic assigned** |
| Old revision `--0000001` | `Running`, 0% traffic — and it is what served every request |
| First request after the deploy | 200, after **42.8s** (scale from zero) |
| `/ready` | still `20260922174809_AddItemPriority` — the failed migration applied nothing |
| Data | all three rows intact, including the duplicates |

**The migration itself behaved exactly as designed.** One transaction, rolled back whole, a
single legible first line in the init container log:
`[migrate] FAILED: 23505: could not create unique index "IX_items_title"`. Nothing was
half-applied, the old code kept serving, and the data was untouched. That part is the design
working.

**The deployment pipeline is what failed.** At `min_replicas = 0` there is no replica to start
at apply time, so nothing exercises the new revision and `apply` reports success. The app is
then "successfully deployed" and broken, and the first person to visit it discovers that — after
waiting 42 seconds. For the §2 persona that is the entire failure mode in one sentence.

Three consequences:

1. **A deploy needs a post-apply readiness gate** that forces a replica, waits, and asserts
   `/ready` reports the migration the release expects. Without it, "the pipeline went green" is
   not a statement about the application at all. See D-22.
2. **`az containerapp logs show --container migrate` cannot find the init container**
   (`Could not find container`). The one line that explains the failure is reachable only by
   querying Log Analytics directly (`ContainerAppConsoleLogs_CL | where ContainerName_s ==
   "migrate"`). Whatever surfaces failures to a human has to know that.
3. **Traffic weight lies.** The portal and CLI show 100% pointed at a revision that never ran.
   Only `runningState` distinguishes a working deployment from this one.

**Also learned, and reassuring:** EF's `AlterColumn` with a `defaultValue` backfills existing
nulls, so making a column non-nullable does *not* fail against live data. That class of change
is safer than expected — the dangerous ones are uniqueness and foreign keys, where existing data
decides.

**One rule is now known to be wrong.** `scripts/check-migrations.sh` forbids editing a migration
that is *merged*. The migration that failed here was merged and never applied, so fixing it
forward is blocked by the guard, and recovery needs a human override. The rule should be
"immutable once **applied**", which the pipeline cannot currently tell. See OQ-21.

## 17. Nothing tells anyone where the app is

The apply produces `app_url` as a Terraform output and then drops it. Finding the running
application means opening the Actions log or the Azure portal — both closed to the §2 persona,
and both awkward for us. The deployment PR is the natural place to post it, and the readiness
gate in D-22 has to fetch the URL anyway.

## Open questions this run has NOT answered

- ~~Whether the module actually works~~ — answered in finding 9: `/ready` confirmed the private DNS and delegated subnet path.
- **How long prompt-to-running-app takes.** Half answered: prompt-to-PR is **4m 11s** (finding 14). Prompt-to-*running* is still unknown, because PR #7 was deliberately not merged — Phase 4a ends at a green plan.
- Whether teardown is clean
- ~~Whether a failed migration is caught at deploy time~~ — answered in finding 16: it is not, at `min_replicas = 0`.
- **The escape hatch:** touched, not answered (finding 14). We now know the agent refuses gracefully and explains itself when a request exceeds the module. We still do not know what the *product* does at that moment, which remains the hardest question in the idea.
- **What agent cost looks like at scale** (finding 14, OQ-18). One run is $0.65 of shadow API cost, dominated by context rather than output, and a follow-up question costs about as much as the original work. Untested: whether trimming the carried context after the tfvars is written materially changes that.
