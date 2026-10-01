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

**Correction, 23 September: this finding was wrong as written.** `apply.yml` already posted
`app_url` as a comment on the merged deployment PR (#9 and #10 both have it). What was missing
was any report at all when a deploy *failed*. The readiness gate now comments on both outcomes
(finding 19). The §2 point stands: a PR comment is still not somewhere the customer looks.

## 18. Teardown outlived its credential — twice, identically

Destroying the stack on 22 September failed, and re-running it failed the same way. Both runs
ended with three errors that all named authorization:

```
githubAssertion: cannot request token: Get "https://run-actions-1-azure-eastus...
/idtoken/...": giving up after 1 attempt(s): context deadline exceeded
```

**The authorization error is the symptom, not the cause.** Read the timings instead: the same
three resources — `azurerm_postgresql_flexible_server_database`,
`azurerm_postgresql_flexible_server_active_directory_administrator` and
`azurerm_postgresql_flexible_server_configuration.ssl` — sat in `Still destroying...` for
**over 30 minutes in both runs**. Terraform was stuck polling Azure operations that never
completed; when it eventually needed to renew the short-lived OIDC assertion, GitHub's id-token
endpoint was no longer reachable for that job, and the run died reporting the renewal rather
than the hang.

Two things this cost, both of which generalise:

1. **A failure whose message points at the wrong layer.** Anyone reading these errors starts
   debugging federated credentials — which are fine, and which were fine in the apply ten
   minutes earlier. The evidence that matters (30 minutes of no progress on three resources) is
   several hundred lines further up, in lines that all look like routine progress.
2. **Retrying is not a recovery path.** The second run repeated the hang exactly. Recovery was
   `az group delete -n rg-demo-dev --yes --no-wait`, followed by one more `destroy.yml` run to
   let Terraform refresh, see 404s, and empty its state.

**For the product this is the OQ-14 question with a face on it.** The §2 persona cannot read an
Actions log, cannot tell a hang from an auth error, and cannot run `az group delete`. A teardown
that half-works and then reports the wrong reason is exactly the class of operation that needs
either a supervised recovery path or a design that cannot get into this state.

**Unanswered, and worth knowing before the next teardown:** why those three child resources hang
at all. Candidates: the Entra administrator delete polling a principal whose identity is already
gone, or the server having a pending operation from the failed revision. Deleting the resource
group succeeded immediately, so whatever it is lives in the per-resource delete path, not in
Azure's ability to remove the resources.

## 19. The readiness gate works — by asking the platform, not the app

D-22 as first written had the gate poll `/ready` until it reported the migration the release
expected. That cannot catch finding 16: after a failed deploy the request is answered by the
**old** revision, which has nothing pending and reports the old migration as current. Anything
the app says about itself describes whichever revision is serving. The gate
(`aaas-deployments/scripts/readiness_gate.sh`) asks Container Apps instead: send requests until a
replica starts, then require the new revision's `runningState = Running` **and**
`latestReadyRevisionName` = that revision, and only then `/ready` → `database: ok`. No change to
the app, the image or the module, and no need for the pipeline to know any migration ID.

Proven on 23 September, three deploys, same stack:

| PR | Change | Gate | Observed |
|---|---|---|---|
| #11 | create at `a708ec8` | **green**, 11s | `Running`/`Healthy` on the first check |
| #12 | `43d8be2` (unique index) over two rows titled `Same title` | **red** | `Activating` for **293s**, then `ActivationFailed`, replicas 0. PR comment named `[migrate] FAILED: 23505: could not create unique index "IX_items_title"` about a minute later. Old revision served throughout; data intact |
| #13 | revert to `a708ec8` | **green**, 37s | `Activating` 20s, `Running` at 33s |

What it taught:

1. **A failing init container is retried for about five minutes** before the revision is
   declared `ActivationFailed` (platform events show `migrate` exiting 1 more than once). A gate
   with a shorter timeout still goes red, but reports "timed out" instead of the cause. The
   default is now 600s. Red therefore arrives about six minutes after the apply, which is
   acceptable only because the old revision keeps serving meanwhile.
2. **Log Analytics filtered by `RevisionName_s` returned another run's output too**: a successful
   migrate ("1 pending: AddItemPriority … schema is current") alongside the failure, with
   stack-trace lines that share a timestamp interleaved. Unexplained. The first PR comment
   therefore read as if the migration passed and failed at once. The PR now gets only the
   runner's own `[migrate]` lines plus platform events; the raw rows stay in the Actions log.
   This is the second time the one legible line had to be dug out of noise on purpose — the
   runner printing a single `FAILED:` first line (D-21) is what made it possible.
3. **Revision names are not a sequence.** The create produced `ca-demo-dev--o95wr7a`; the
   updates were `--0000001` and `--0000002`. Read `latestRevisionName`, never derive it.
4. **Recovery by revert is uneventful.** A deploy after an `ActivationFailed` revision behaves
   like any other. The migration had rolled back whole, so the old image met the schema it
   expected. That is what makes fail-and-stop plus a revert PR a complete rollback — for
   expand-only migrations (D-21). A migration that half-applied would break this.
5. **A fine-grained token without the Workflows permission cannot push to `.github/workflows/`**
   — GitHub refuses server-side. This session's token (Contents + Pull requests, one repo)
   could push deployment changes and the gate script but not `apply.yml`. That is exactly the
   boundary the agent's `GH_TOKEN` should have: deterministic, enforced by GitHub, not by our
   allowlist (D-17). Worth asserting in `verify-isolation`, which cannot currently see token
   permissions.

## 20. Teardown: the child-resource hang is gone; a green destroy can still be a lie

Finding 18 happened a third time on 23 September: `destroy` sat in `terraform destroy` for 23
minutes and failed. The cause is now clear enough to act on without being proven. The Postgres
server's three child resources (database, Entra administrator, `require_secure_transport`) have
no dependents, so Terraform deletes them first and **in parallel** against a server that takes
one management operation at a time; the server, network, DNS and resource group all wait behind
them. Deleting the resource group, which removes the server whole, took seconds. None of the
three deletions does anything useful.

**Fix** (`destroy.yml`): before `terraform destroy`, `terraform state rm` every resource of those
three types, so Terraform deletes the server directly. Tested 24 September on a fresh stack:
state step 18s, `terraform destroy` **success in 24m57s**, no hang, no OIDC error.
**Still slow:** 25 minutes is close to where the earlier runs died on token renewal. Which
resource took the time is unknown (the job log needs repo admin to read). If the next destroy
fails the same way, the server delete itself is the slow part, not its children.

Getting there cost an evening, and taught three things that generalise:

1. **A destroy can go green while everything still exists.** After the hung run, a cleanup
   destroy "succeeded" in **18 seconds**. Terraform's state no longer listed the server, so it
   deleted nothing, while `psql-demo-dev-e9fdbf` sat in Azure unchanged. Same shape as finding
   16 in the other direction: the pipeline reports what Terraform did, not what Azure holds.
   Needs a post-destroy assertion (`az group exists` must be `false`), not built yet.
2. **`az group delete --no-wait` returns before anything is deleted.** An apply started 90
   seconds after one wrote into a resource group Azure was halfway through deleting, and failed
   with `appdb ... already exists - needs to be imported`. Never run a pipeline job against a
   resource group in `Deleting`; wait for `az group exists` to print `false`.
3. **The state-rm fix has a failure mode.** If a destroy fails *after* the three are dropped from
   state, the next **apply** tries to create them, and the database create refuses because
   `appdb` exists. Recover with another destroy, never an apply. Written into `destroy.yml`.

## 21. Prompt to running app: 18m 43s on create, 8m 23s on update

26 September 2026, Phase 4c. The agent wrote application code for the first time and it reached
Azure through the whole chain: prompt → app PR → CI → merge → image → deployment PR → plan →
merge → apply → readiness gate green. Two runs against `aaas-app-demo`, both from the harness
container on a laptop, both with `--non-interactive`, and no human edits to any code. The model
was the CLI default (the commit trailers say Sonnet 4.6).

| | Run 1 - create | Run 2 - update |
|---|---|---|
| Run id | `20260926T142507Z` | `20260926T144922Z` |
| Brief | `item-done.md`: tick items off, filter to open ones | `item-open-fix.md`: open items go missing past 100 |
| Agent: prompt → PR | **4m 11s**, 55 turns | **2m 43s**, 27 turns |
| Cost (shadow) | **$0.90** | **$0.43** |
| Policy refusals | 3 | **0** |
| Tool calls | 26 Bash, 9 Read, 9 TodoWrite, 6 Edit, 3 Write | 12 Bash, 8 Read, 3 Write, 2 Edit, 1 Glob |
| App CI | 1m 16s, green first push (PR #4) | green first push (PR #5); merged after 1m 28s, **before `build` finished** - see below |
| Release → deployment PR | 58s (#14) | 57s (#15) |
| Plan on deployment PR | 1m 30s | ~1m 30s |
| `terraform apply` | 8m 30s - **full create**, the stack had been destroyed | **49s** - image bump only |
| Readiness gate | 12s | 39s |
| Operator merges (me) | ~1m 40s | ~20s |
| **Prompt → running** | **18m 43s** | **8m 23s** |

Run 1 added a `MarkItemDone` migration (`is_done boolean not null default false`, `done_at`
nullable) - expand-only without being told twice. `/ready` reported it live; marking an item done
and `GET /items?open=true` worked against the deployed app. The data written after run 1 survived
run 2's deploy.

**The number that matters is the update: ~8 minutes, of which the agent is a third and Azure one
minute.** The rest is CI and two merges that were a human (me, via the API) reacting to a green
check. The create case is dominated by provisioning Postgres and is paid once per estate. For a
non-technical user, eight minutes from "open items go missing" to fixed is a plausible product
loop; the remaining time is the pipeline, not the model.

### The harness made the agent solve an auth problem, and it solved it the wrong way

`gh repo clone` authenticates the clone and nothing after it. The checkout had no credential
helper, so the runbook's `git push` failed with `could not read Username`. The agent spent five
turns on it (`git remote -v`, `gh auth status`, `gh repo set-default` - refused, `git config
--global credential.helper` - refused) and then pushed to
`https://main0034:${GH_TOKEN}@github.com/...`. The policy allowed that: `${VAR}` is expansion, not
command substitution, so neither regex saw it. Nothing leaked - the transcript holds the variable
name, and `gh auth status` masks the token - but the token went into git's argument list, and the
agent's own conclusion was to route around an authentication failure.

In 4a the push worked. The deployments clone must have been pushed some other way that run; this
was not investigated because the fix is the same either way.

**Fixed in the harness, not the agent** (`aaas-agent` `23f6fd4`): every checkout gets
`!gh auth git-credential` as its credential helper, and the policy refuses any reference to
`GH_TOKEN` or `gh auth token`, telling the agent to stop and report an auth failure instead.
`create-app.md` says the same. Run 2 pushed first time.

### The second heredoc

Refusal two was `git commit -m "$(cat <<'EOF' ...)"` - the same shape as finding 14's PR body,
because the runbook said `git commit -m "..."` and a multi-line message with a co-author trailer
is what the model reaches for. The runbook now says `git commit -F /tmp/commit-msg.txt`, and run 2
used it. Both refusals in the first run were runbook or harness problems; none was the agent
misbehaving. That is now three of four refusals across two first runs.

### Green CI, wrong code

Run 1's `GET /items?open=true` was `OrderByDescending(id).Take(100).Where(!IsDone)` - the filter
after the limit, so once the latest 100 items are done the open ones vanish. CI was green: every
test the agent wrote asserted that a route answers 503 without a database, which proves the route
exists and nothing about what it returns. The runbook's "tests pass with no database" rule had
been satisfied in the least useful way.

`create-app.md` now says so and asks for behaviour in a testable method. Run 2 was the fix, briefed
the way a user would report it ("open items go missing once there are a lot"). The agent diagnosed
it correctly, moved the filter into an `IQueryable` extension and tested that against 200 in-memory
items. **The test is still weaker than it claims:** it composes `WhereOpen().OrderBy...Take(100)`
itself, so it would pass with the old ordering left in `Program.cs`. Testing the logic moved the
bug's hiding place from the query to the endpoint's composition of it. What would catch it is a
test through the endpoint against a real database - which CI's throwaway Postgres already
provides for migrations and not for tests. Relevant to Phase 5 and to how much a green check
should be trusted before an automatic merge (OQ-5).

### `aaas-app-demo` does not require green checks to merge

Run 2's PR was merged while its `build` check (image build, smoke test, migrations applied twice
to a real Postgres) was still running - by me, through a monitoring script with a syntax error
that fell through to the merge. `build` went green 20 seconds later, so nothing broken shipped.
But GitHub accepted the merge: the repo has no ruleset on `master`
(`GET /rules/branches/master` → `[]`). The deployments repo has its `gate`; the app repo, which is
where the agent's code lands, has nothing enforcing that CI passed. The token used also has admin
on every repo, so a ruleset would not have stopped *this* merge unless admin bypass is off.

### The token

One fine-grained PAT served both the agent and the operator. Probed, not assumed: pushing a file
under `.github/workflows/` returns **403 "Resource not accessible by personal access token"**, so
finding 19's boundary holds. But the same token has Contents write on all six repos, including
`aaas-agent` - so for these runs the agent *could* write to the harness that constrains it, which
the `aaas-agent` README says it cannot. Two tokens (agent: deployments + one app repo; operator:
everything) restore the property.

### Running it from a linked cloud session

These runs were driven by Claude from a cloud session linked to the laptop. Three things shaped
how, and each cost time once:

- The linked shell is a Linux VM with the `aaas` folder mounted and **no Docker**; Terminal can be
  granted to computer use in click-only mode, so commands cannot be typed into it. The build and
  the run therefore went into one script (`phase4c.sh`, logged through `script(1)`) that Martin
  started once per run and Claude followed from the log.
- A Terminal tab without Docker on its `PATH` failed the first start (`docker: command not found`).
- `git commit` from the mounted folder leaves `index.lock`, `HEAD.lock` and `tmp_obj_*` behind,
  because the mount refuses deletes. Deletion had to be granted and the locks removed after every
  commit, or Martin's next git command fails.

## 22. Side quest: six agent runs in one afternoon, and what they said about the loop

27 September 2026. **Not golden-path work** - a local-only "notebook" app (draw and type on a
sheet; questions go to the local `claude` CLI) built by the agent on `aaas-app-demo` branch
`feat/notebook`, [PR #6](https://github.com/main0034/aaas-app-demo/pull/6), which must never be
merged. Recorded only for what it taught about the agent loop. The harness moved to uv the same
day ([aaas-agent #1](https://github.com/main0034/aaas-agent/pull/1), image build verified).

| Run | Brief | Result | Wall clock | Cost (shadow) |
|---|---|---|---|---|
| `20260927T125116Z` | notebook, 60-turn default | **hit `max_turns`** one step before its final test pass; nothing pushed | 17m 24s | $2.54 |
| `20260927T131219Z` | same, `--max-turns 150` | PR #6, 69 turns, ~1,800 lines | 11m 14s | $2.10 |
| `20260927T142734Z` | fix: missing CLI returns 500 | pushed to the same branch, commented on the PR | **2m 44s** | **$0.52** |
| `20260927T152817Z` | handwriting (image → CLI → text) | pushed, 78 turns | 21m 55s | $3.35 |
| `20260927T155340Z` | live preview + pending-ink bug | **hit the subscription's 5-hour limit**; nothing pushed | 11m 31s | $1.31 |
| `20260927T180921Z` | same, rerun | pushed | 24m 57s | $3.48 |

About **$13.30** of shadow API cost in one afternoon, $3.85 of it (29%) thrown away.

**A capped run loses everything.** The push is the runbook's last step, so a run stopped by
`max_turns` or by a rate limit leaves nothing on GitHub; the harness reports `exit 1` and "No pull
request URL", with the cause only in `report.md`. `max_turns` is a per-brief sizing decision, not a
constant - and the harness should say *why* a run ended on its last line.

**The subscription is a capacity ceiling, not only a licence problem (OQ-18).** A day of
iterating on one app exhausted a personal plan's 5-hour window mid-run.

**Green was not correct, twice more** (finding 21 again). A missing `claude` binary returned
HTTP 500 with the question saved half-done; a reload made old ink "pending", so the next read
swept it in. 49 and 94 green tests respectively; both were found by running the app against a
real Postgres and the real CLI, in a browser. **This is not Phase 5 fix-forward** - CI never went
red and a human found the defect. It does show that a follow-up from a precise description of an
observed failure is cheap: a fresh run with a focused brief, $0.52 against $2.10 for the original,
which is finding 14 ("the cost is context") seen from the other side.

**A rule nobody checks gets broken.** The preview run added
`Microsoft.EntityFrameworkCore.InMemory` to the tests, which `AGENT.md` forbids in so many words.
CI passed. Per D-17 the fix is a check, not a sterner sentence: a CI step that fails on that
package reference. It lives in the template's workflow, which only a human may edit.

**Local-run friction worth knowing:** a Rider run configuration committed with a Docker
"server" name fails silently on any machine without a Docker connection of that exact name
(`Server Docker not found`, visible only in `idea.log`); a shell-script pre-launch step has no such
dependency. And `claude setup-token` - how the harness token was made - does not log the CLI itself
in; a subprocess that calls `claude` needs `/login` once.

## 23. Fix-forward works: red → fresh session → green in one round, $0.38 a round

28 September 2026, Phase 5. The harness now waits for a PR's checks and, on red, gives a
**fresh** session the request, the diff stat and the trimmed failure log
([aaas-agent #2](https://github.com/main0034/aaas-agent/pull/2), `--fix-rounds 2`). The loop
lives in the harness, not the agent's turn, so waiting costs no tokens. It never merges.
Brief: `briefs/item-search.md` (search items by word in title or note, combinable with
"only open"). Model: CLI default.

| Run | Setup | Result | Wall clock | Cost (shadow) |
|---|---|---|---|---|
| `20260928T171215Z` | normal runbook | green first push (PR #8, closed) | 8m 21s | $0.80 |
| `20260928T172139Z` | `--skip-local-checks` | green first push (PR #9, closed) - **invalid, see below** | 11m 4s | $1.18 |
| `20260928T173337Z` | `--skip-local-checks`, one run per container | **red → 1 fix round → green** (PR #10, merged) | 7m 7s | **$0.76** |

The run that counts, per round:

| Round | Agent | Model turns | Cost | CI verdict | CI wait |
|---|---|---|---|---|---|
| 0 - initial | 2m 47s | 17 | $0.38 | **red** (`test`) | 42s |
| 1 - fix, fresh session | 2m 38s | 22 | $0.38 | green | 57s |

Then: operator merge 1m 47s after green → deployment PR #17 in 73s → plan 1m 14s → merge →
create apply **failed** after 9m (below) → recovery PR #18 → apply 44s → **readiness gate green**
(46s). **Prompt → running: 26m 06s**, of which ~13m was the failed create and its recovery.
Search, case-insensitivity and search + open-only were checked by hand against the deployed app.

**The failure was real, not staged.** The first push used
`Contains(term, StringComparison.OrdinalIgnoreCase)`, which EF Core cannot translate. The
endpoint test got 500 instead of 503, because the exception is thrown while building the query,
before the database is touched, and `DatabaseUnavailableHandler` only maps database failures.
In production the same line would have made `GET /items?q=` return 500. The fix round switched to
`ToLower().Contains()`, correctly, and explained why. The normal-runbook run hit the same bug
locally and fixed it before pushing, which is what `--skip-local-checks` removes.

**A fix round costs what the original cost, not a fraction of it.** $0.38 against $0.38. Finding 22's
$0.52-vs-$2.10 was a small fix to a large feature; here both halves are small, and the fixed cost
of a session (runbook, AGENT.md, reading the code) dominates. Budget a fix round at roughly one
more run, not a percentage.

**The first choice of failure did not produce one.** The plan was a CI check banning the EF
in-memory provider (now on `master` in the app and the template, `ci: fail on a fake database in
tests`), plus a brief that asks for endpoint tests. The agent followed `AGENT.md` both times and
never reached for the package. The check is still worth having; it just is not a lever for making
an agent fail.

### The harness leaked one run into the next

`run.sh` mounted all of `runs/` into the container. Run 2 listed `/work/runs`, read run 1's
workspace, and copied its already-formatted, already-fixed code - so skipping local checks changed
nothing, and the measurement was void. It is also a boundary problem: every run could read every
earlier run's transcript. Fixed (`be1c938`): `run.sh` picks the run id and mounts only
`runs/<id>`; `verify-isolation` refuses to start if `/work/runs` holds anything else. Same lesson
as finding 2: the container's mounts are the boundary, so assert them.

### The fix round got no log, and fixed it anyway

`gh api .../jobs/<id>/logs` refuses to print a log that contains terminal escape sequences, and
`dotnet test` colours its output. The harness caught the error and put *the error message* in the
brief where the log should have been. The agent then tried `gh api ... --allow-escape-sequences`
itself (refused by the policy - it had read the flag in that message), fell back to running the
tests locally, reproduced the failure and fixed it. Graceful, but luck: a CI-only failure (the real
Postgres migration apply, the smoke test) cannot be reproduced locally. Fixed: the harness passes
the flag and strips the sequences. Trimming now anchors on the failing step
(`##[group]Run …` to the first `##[error]`) rather than the word "error", which the first version
matched in the *script text* of the new CI step and so handed over 40 lines of git checkout. On the
real log from this run the trimmed version is 150 lines, starting at `Run dotnet test`.

### A create apply failed on `appdb`, and an import recovered it

The create for deployment PR #17 built all 14 resources and then failed on the last:
`azurerm_postgresql_flexible_server_database.this` - "a resource with the ID …/databases/appdb
already exists - to be managed via Terraform this resource needs to be imported into the State".
The server had been created 1 second earlier by the same apply; the database create started in the
same second as the server's SSL configuration and AAD administrator. **Cause not established**;
the likely shape is concurrent child operations on a new Flexible Server and a create that
succeeded in Azure while the provider saw an error. Finding 21's create of the same stack worked.

**Correction, 29 September (finding 24):** the cause was the application, not the Postgres
children racing each other. The Container App's `migrate` init container created `appdb` itself
(EF Core `Migrate()` creates a missing database) the moment its identity became administrator.
Fixed in `app-stack` v0.3.2.

A re-run fails the same way, and destroy-and-recreate costs ~35 minutes. Instead: an `import`
block for the database in `deployments/dev/demo/imports.tf` (PR #18, plan `1 to import, 0 to
add`), applied in 44s, readiness gate green; then removed again (PR #19), because on the next create
it would try to import a database that does not exist yet. That is the fourth Postgres-child
failure (findings 18, 20, and now this one on create), and the first with a recovery that took
minutes. A §2 user cannot do any of it: OQ-14.

### Smaller things

- **`aaas-app-template` could not merge any PR**: its ruleset allowed only rebase, the repository
  only squash. Martin fixed the settings on 28 September.
- **The operator token cannot start a workflow** (`workflow_dispatch` → 403), so `destroy` still needs
  the GitHub UI.
- The agent tokens behaved as intended: the agent token is refused on `aaas-agent` and `aaas-docs`
  (push dry-run), and the Workflows 403 is intact for both tokens.
- One false policy refusal: a `find` over `/root` containing `nuget` in a path was refused as
  `dotnet nuget`.
- **Tests still prove less than they claim** (finding 21). The brief asked for tests that go
  through `/items` and show which items come back. The agent wrote a 503 test through the
  endpoint and behaviour tests on an in-memory `IQueryable`, because `AGENT.md` says tests run with
  no database. The brief and the template contradict each other, and the template wins. That
  503 test is also what caught the bug, by accident.

## 24. Phase 6: the chain runs from nothing, except the create - and the app was racing Terraform

29 September 2026, Phase 6. One run from a destroyed stack, not recorded (Martin: "if it works it
works"). Brief `item-due.md`: optional due dates on items and a list of overdue ones, which needs a
migration. Run `20260929T175630Z`, `--fix-rounds 2`, started by `phase6.command`.

| Step | Time (UTC) | Duration |
|---|---|---|
| Harness start → app PR #11 opened | 17:56:30 → ~18:02:00 | **5m 25s** agent, 37 turns, **$0.79**, 0 refusals |
| App CI (`test`, `build`) | 18:02:04 → 18:02:57 | 53s, **green on first push** - no fix round |
| Operator merge (me, via the API) | 18:08:04 | 5m wait: my polling interval, not work |
| Release → deployment PR #21 | → 18:09:04 | 60s |
| Plan on #21 | 18:09:07 → 18:10:19 | `Plan: 14 to add` - a true create |
| Merge → apply | 18:10:41 → 18:19:57 | **9m 16s, failed** on `appdb` "already exists" |
| Diagnosis, `app-stack` v0.3.2, recovery PR #22 | → 18:24:45 merged | ~5m |
| Apply #22 (import + v0.3.2) → readiness gate green | 18:24:47 → 18:27:28 | 2m 41s |
| **Prompt → running** | 17:56:30 → 18:27:28 | **30m 58s**, of which ~5m operator latency and ~7m failure and recovery |

Checked against the live app: `/ready` → `migration: 20260929180049_AddItemDueDate`. With six items
created through the API, `GET /items/overdue` returned exactly the two past-due open ones, oldest
first; the item due today, a done item past its date, a future item and an undated item were absent.
Clearing a due date (`PUT /items/{id}/due-date`) removed that item from the list.

### The create failure was the app, not Postgres

v0.3.1 (this morning) serialised the three Postgres child resources, on finding 23's guess that
concurrent operations on a new server were the cause. The create failed identically, and this time
the apply log's timestamps showed why:

```
18:18:29  azurerm_container_app.this: Creating...          (server ready; SSL setting starts too)
18:18:50  azurerm_container_app.this: Creation complete
18:19:47  ..._active_directory_administrator.app: Creation complete
18:19:48  azurerm_postgresql_flexible_server_database.this: Creating...
18:19:53  Error: ... databases/appdb already exists
```

The Container App had no dependency on the database, only on the server's FQDN. Its first revision's
`migrate` init container started at once and retried until the app's identity became the server's
administrator. **EF Core's `Migrate()` creates the database when it is missing**, and the identity is
administrator, so the app created `appdb` in the second before Terraform did. Finding 21's create
survived the same race by luck of timing.

**Fix: `app-stack` v0.3.2** - the Container App `depends_on` the database and the administrator. The
v0.3.1 ordering is kept (a Flexible Server takes one management operation at a time) but was not the
cause. **Not yet proven by a clean create.** The deployment pins v0.3.2 since #22.

The general form, for OQ-14: *anything the platform starts during an apply can create state
Terraform expects to create itself.* The app's startup path is part of the infrastructure's create
path whenever the app holds rights to create things - and here it is the database administrator
(the POC's deliberate coarseness, §4.3 of the module README).

### Recovery took three minutes because it was the second time

Import block + pin bump in one PR (#22: `1 to import, 0 to add, 1 to change`), green gate, then a
PR to remove the import (#23). The same recovery as #18/#19, by the same operator, with the plan
shape already known. For the §2 user it is still impossible (OQ-14).

### Smaller things

- **The pin bump was merged with `[skip ci]`** (#20) so that a module change to a destroyed stack did
  not create it outside the demo. It works because the apply triggers on push; it also means `master`
  briefly described a stack no apply had seen. Acceptable for a destroyed stack, never for a live one.
- **A permanent in-place diff:** every apply after a create shows `azurerm_container_app.this` changing
  `workload_profile_name = "Consumption" -> null` plus empty `args`/`command` lists. Seen on #18,
  #22 and #23; applies are green and the app keeps running. Plans run with `-refresh=false`
  (finding 11), so it never settles. Harmless so far; setting `workload_profile_name = "Consumption"`
  in the module would likely remove it.
- **Tests, the third time** (findings 21, 23): the brief asked for tests through the endpoints. The
  agent wrote an in-memory `IQueryable` test that composes `WhereOverdue(...).OrderBy(...)` itself -
  so it would pass with the endpoint's ordering wrong - and two 503 checks. The code was right; the
  tests could not have shown it. The template decides test strength, not the brief.
- **The `destroy` workflow now verifies against Azure** (`78922bc`): it logs in again after the
  destroy and fails unless `az group exists -n rg-<name>-<environment>` is `false` within 10 minutes.

---

## 25. Tests that prove behaviour: endpoint tests against Postgres, and the agent writes them unprompted

1 October 2026. Template and demo tests now call routes against a real Postgres. One agent run
with a brief that never mentions tests. Before it: the v0.3.2 clean create.

### The clean create: proven, and 22 minutes of it was one resource

`apply` run 36893894974 (dispatched from the UI on the destroyed stack): `Plan: 14 to add`,
**14 added, no "already exists", no import, no re-run**. The order finding 24 asked for:
`azurerm_postgresql_flexible_server_database.this: Creation complete after 16s` *before*
`azurerm_container_app.this: Creating...` (21s). Readiness gate green, `/ready` → `database: ok`,
`managed-identity`, `AddItemDueDate`. `POC-PLAN.md` §9 is now fully ticked.

The apply step took **21m 46s**, of which the **Container App Environment alone was 18m 27s**. The
Postgres chain ran beside it in ~7m 41s (server 6m 8s, SSL 15s, AD admin 1m 2s, database 16s). The
module's serialisation is not the cost; the workload-profile environment is, and it varies run to
run (finding 21's whole create was 18m 43s including the agent). Budget a create at 10–25 minutes.

### The fixture

- `TEST_POSTGRES` (a connection string) is the only switch. CI sets it on the `test` job, which now
  has a `postgres:16` service. Unset, endpoint tests **skip** with a message.
- An assembly fixture creates `app_test_template_<guid>` once and applies the app's own migrations to
  it. **Each test** clones its own database from it (`CREATE DATABASE … TEMPLATE`) and drops it after.
- `EndpointTest` is the base class: `SeedAsync(rows…)`, `Client`, and an inherited
  `Category=Endpoint` trait (a custom `ITraitAttribute` with `Inherited = true` — a `[Trait]` on an
  abstract base class is *also* inherited in xUnit v3, but the custom attribute makes it explicit).
- CI runs two steps: `--filter-not-trait "Category=Endpoint"`, then
  `--filter-trait "Category=Endpoint" --fail-skips on --minimum-expected-tests 1`. Proven locally:
  no database → skip becomes a failure (exit 2); a filter that matches nothing → exit 8. A green
  `endpoint tests` step therefore means at least one route test ran against Postgres.

**Per test, not per class — changed after Martin had chosen per class.** Ten natural tests ("seed two
rows, expect exactly two back") in one class with a shared database: **10 of 11 failed** on each
other's rows. Clone ~0.1s, drop ~0.15s; 11 endpoint tests took 5.8s locally, 8 took 3.2s in CI. An
agent would have hit the per-class failure on its first test file.

xUnit v3 / MTP spelling that cost two attempts: options go straight after `dotnet test` (no `--`),
and `--fail-skips` takes a value (`on`). Without the value the run exits 5 with zero tests.

### The planted bugs

Demo: the 503 checks and in-memory `IQueryable` tests replaced by 8 endpoint tests (PR #12). Each
bug planted in the route code, run, reverted:

| Planted bug | New suite |
|---|---|
| overdue list ordered newest-due first | red |
| an item due today counted as overdue (`<=`) | red |
| filter applied after `Take(100)` | red |
| search case-sensitive | red (2 tests) |
| list oldest first | red (3 tests) |
| **old suite, filter after `Take`** | **15/15 green** |

The old suite contained a test *named* for the filter-before-`Take` bug. It composed the query itself
over a `List<Item>`, so the route could do anything. That is finding 21/23/24's weakness in one line.

### The agent run

Brief `briefs/item-priority.md`: open items ordered by priority, no-priority last, ties newest first,
optional cutoff. **No mention of tests.** Run `20261001T180920Z`, `session-1001.command`.

- PR #13 in **155s, $0.45**: `/items/priority`, two query extensions, and
  `ItemPriorityEndpointTests` — **4 endpoint tests, unprompted**, asserting exact titles in order with
  rows that must not come back (a done item, priorities below the cutoff, unprioritised items).
- CI **red**: `Expected "p2", Actual "p1"`. The *test* was wrong (`["p2","p1"]` for "priority 2 or
  better"); the code was right. The agent could not have seen it: endpoint tests skip in the harness.
- Fix round 1 (fresh session, 101s, $0.29): corrected the expected order, pushed, **green**; CI ran
  **12 endpoint tests, 0 skipped**. Total **7m 20s, $0.74**, 1 policy refusal. Not merged — D-23.

Read independently: the fix was correct (the brief, and the same file's first test, both put
priority 1 first). **But the fix round's move was to edit an assertion until CI agreed with the
code.** Here the code was right. With the code wrong and the test right, the same move turns a
caught bug green. This is the new risk OQ-5 has to account for, and a fix round is where it lives.

Cost of writing tests the agent cannot run: one fix round (~$0.29 here). As argued at the start of
the session, cheaper than a Postgres in the harness — but it means a red endpoint test is more likely
to be a wrong *test* than before.

### Smaller things

- **Policy false positive:** `git commit -F /dev/stdin <<'EOF'` with a message starting `fix:` was
  refused as the command `fix:`. The agent fell back to a file. Same family as the `nuget` path one.
- **Unauthenticated GitHub API is 60 requests/hour per IP, shared with Martin's machine.** Polling a
  run every 10 seconds exhausted it in ~10 minutes and blinded the session for 40. Job logs need
  authentication anyway, API or web. Since this session the linked shell has `gh` with the operator
  token; poll at a minute, not ten seconds.

## Open questions this run has NOT answered

- ~~Whether the module actually works~~ — answered in finding 9: `/ready` confirmed the private DNS and delegated subnet path.
- ~~Whether the agent can fix its own failed check~~ — answered in finding 23: yes, in one fresh round, for $0.38. Finding 25 saw a CI-only failure it could not reproduce locally, fixed in one round - by editing a test's assertion, which was right that time. Not yet seen: a second round.
- ~~How long prompt-to-running-app takes~~ — answered in finding 21: **8m 23s** for a change to an existing app, **18m 43s** when the stack has to be created. Not yet measured: a new app from nothing (new repo, new deployment), which needs repo provisioning (OQ-15).
- Whether teardown is clean — partly (finding 20): the child-resource hang is fixed, and since 29 September (finding 24) `destroy` fails unless Azure confirms the resource group is gone. A destroy still takes ~25 minutes.
- ~~Whether a create from nothing succeeds without help~~ — answered in finding 25: yes, on `app-stack` v0.3.2, 14 added with no import. 22 minutes, 18 of them the Container App Environment.
- ~~Whether a failed migration is caught at deploy time~~ — answered in finding 16: it was not, at `min_replicas = 0`. Since finding 19 it is, by the readiness gate.
- **The escape hatch:** touched, not answered (finding 14). We now know the agent refuses gracefully and explains itself when a request exceeds the module. We still do not know what the *product* does at that moment, which remains the hardest question in the idea.
- **What agent cost looks like at scale** (finding 14, OQ-18). One run is $0.65 of shadow API cost, dominated by context rather than output, and a follow-up question costs about as much as the original work. Untested: whether trimming the carried context after the tfvars is written materially changes that.
