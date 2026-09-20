# AaaS — setup runbook

Everything in the four repos is written. This is the list of things **you** have to do, in order, because they need credentials or a browser.

Roughly 60–90 minutes, plus 10–15 minutes of waiting during the first apply.

---

## Step 1 — Azure bootstrap (~15 min)

Creates the state storage account, two service principals, and their GitHub OIDC federated credentials.

```bash
az login
az account list --output table          # pick a subscription
cd aaas-infra-modules
./bootstrap/bootstrap.sh <subscription-id>
```

You need **Owner** on the subscription — the script assigns roles, which `Contributor` alone cannot do.

It prints a block of `gh variable set` commands at the end. Keep that output.

The script assumes a default branch of **`master`**. Override with `DEFAULT_BRANCH=main ./bootstrap/bootstrap.sh <id>` if that ever changes. This value becomes the subject of the OIDC federated credential and Azure matches it as an exact string — a mismatch fails at token exchange with `no matching federated identity record found`, which looks like a permissions problem and is not one.

**If an earlier run created a `github-main` credential**, delete it before re-running. Note that an early failure (e.g. the subscription error) stops the script with `set -e` *before* the service principal section, in which case nothing was created and there is nothing to remove — a "does not exist" error here is a pass, not a problem.

```bash
APP_ID=$(az ad app list --display-name sp-aaas-apply-dev --query "[0].appId" -o tsv)
az ad app federated-credential list --id "$APP_ID" -o table
az ad app federated-credential delete --id "$APP_ID" --federated-credential-id github-main || true
./bootstrap/bootstrap.sh <subscription-id>
```

Each line is a separate command. Collapsing the first two onto one line makes `APP_ID=` an environment prefix rather than an assignment, and `$APP_ID` then expands to empty.

### Verify the credentials

Worth doing every time, because a wrong subject is silent until `apply` runs and then reports what looks like a permissions failure:

```bash
for app in sp-aaas-plan-dev sp-aaas-apply-dev; do
  id=$(az ad app list --display-name "$app" --query "[0].appId" -o tsv)
  echo "== $app ($id)"
  az ad app federated-credential list --id "$id" --query "[].{name:name, subject:subject}" -o table
done
```

Expected, for a repository created after 15 July 2026:

```
plan:  repo:main0034@40392502/aaas-deployments@1328777566:pull_request
apply: repo:main0034@40392502/aaas-deployments@1328777566:environment:dev
apply: repo:main0034@40392502/aaas-deployments@1328777566:ref:refs/heads/master
```

### Why apply needs an environment subject

`apply.yml` and `destroy.yml` declare `environment: dev`. **When a job references a GitHub Environment, the OIDC subject becomes `:environment:<name>` and the branch ref is replaced, not supplemented.**

This is a genuinely easy trap: the workflow still only runs on the default branch, so a branch-scoped credential looks obviously correct. It fails with `AADSTS700213`, which reads as a permissions problem.

The script creates both credentials — the environment one is what apply and destroy present today, and the branch one covers any future job on the default branch that does not use an environment.

If you later add environments beyond `dev`, each needs its own credential.

### Immutable subject claims

Those embedded numbers are the owner ID and repository ID, and they are not optional. GitHub repositories created after **15 July 2026** use *immutable subject claims*: the numeric IDs are always present in the `sub` claim and cannot be removed, even with claim customisation.

The reason is sound. Under the old name-only format, deleting a repo and letting someone else claim the name meant they could mint tokens matching your cloud trust policy. Binding the claim to immutable IDs closes that.

The practical consequence is that any guide written before mid-2026 — including earlier versions of this one — produces a credential that never matches, failing with:

```
AADSTS700213: No matching federated identity record found for presented assertion subject '...'
```

which reads like a permissions problem and is not one. `bootstrap.sh` now asks the GitHub API for the IDs, builds the subject accordingly, and **replaces** any existing credential whose subject has drifted. Re-running it is the fix.

If `gh` is not authenticated when you run it, the script falls back to the legacy format and says so. Override explicitly with `OWNER_ID_OVERRIDE` / `REPO_ID_OVERRIDE`.

Note also that the subject is per-repository. If you later add app repos that authenticate to Azure directly, each needs its own credential with its own repo ID.

Below is the output from Step 1:
```
================================================================================
Bootstrap complete.

Set these as repository VARIABLES (not secrets) on main0034/aaas-deployments:

  gh variable set AZURE_TENANT_ID       --repo main0034/aaas-deployments --body "ea5e87d3-8a46-4d7c-b1ff-3ba0bc35efc6"
  gh variable set AZURE_SUBSCRIPTION_ID --repo main0034/aaas-deployments --body "da2df6f3-8103-4209-a882-96fbe2f78977"
  gh variable set AZURE_CLIENT_ID_PLAN  --repo main0034/aaas-deployments --body "052fbde9-dc28-4631-b865-29bf15e581c2"
  gh variable set AZURE_CLIENT_ID_APPLY --repo main0034/aaas-deployments --body "10b5cd32-aae1-491a-9826-35523928518e"
  gh variable set TFSTATE_RG            --repo main0034/aaas-deployments --body "rg-aaas-tfstate"
  gh variable set TFSTATE_SA            --repo main0034/aaas-deployments --body "staaastfstate7e0ad188"
  gh variable set TFSTATE_CONTAINER     --repo main0034/aaas-deployments --body "tfstate"

These are identifiers, not credentials - there is no secret to leak, which is
the entire point of OIDC.

State backend values for backend.hcl:
  resource_group_name  = "rg-aaas-tfstate"
  storage_account_name = "staaastfstate7e0ad188"
  container_name       = "tfstate"
================================================================================

```

**Running this script costs nothing.** Service principals and federated credentials are free; the state storage account holds a few hundred KB. Spend begins when you merge the first deployment PR in Step 8.

It also sets a **$20/month budget alert** on the subscription, emailing `martiningeson@gmail.com` at 50%, 80%, 100%, and on forecast-to-exceed. Override with `BUDGET_AMOUNT` / `BUDGET_EMAIL` env vars if you want different values.

Note that Azure budgets **alert, they do not stop anything.** The forecast notification is the one that gives you time to act; by the time the 100% actual alert arrives the money is already spent.

> A dedicated subscription is worth it here. Clean teardown, honest cost visibility, and no chance of the POC colliding with anything else you run.

---

## Step 2 — GitHub App (~15 min)

One app, used for everything Git-to-Git. There is no Azure credential anywhere in GitHub — that is what OIDC bought us.

Create it at **Settings → Developer settings → GitHub Apps → New GitHub App**:

- **Name:** `aaas-bot` (must be globally unique; add a suffix if taken)
- **Homepage URL:** anything, e.g. your GitHub profile
- **Webhook:** uncheck **Active**
- **Repository permissions:**
  - Contents: **Read and write**
  - Pull requests: **Read and write**
  - Metadata: Read-only (automatic)
- **Where can this app be installed:** Only on this account

Then:

1. **Generate a private key** → downloads a `.pem`. You cannot retrieve it again.
2. Note the **App ID** from the app's General page. This is a 6–7 digit **number**, e.g. `1234567`.

   Do not use the **Client ID** (`Iv23li…`) that sits next to it. It is the more prominent of the two and is the natural thing to grab, but the action signs a JWT whose `iss` claim must be an integer. Using the Client ID fails at runtime with `'Issuer' claim ('iss') must be an Integer`, which does not point back here at all.
3. **Install App** → your account → select these repositories: `aaas-deployments`, `aaas-infra-modules`, `aaas-app-demo`.

### Why an App rather than a PAT

A PAT would work today and become a problem later: it carries your identity, it expires on a schedule you'll forget, and its scope is all-or-nothing. The App is scoped to three named repositories, its tokens live for an hour, and PRs it opens are attributed to `aaas-bot` rather than to you — which matters once you're trying to tell agent-originated changes from your own.

---

## Step 3 — GHCR pull token (~5 min)

Container Apps needs to pull a private image. **Settings → Developer settings → Personal access tokens → Tokens (classic)** → generate with **`read:packages`** only.

> This is the one long-lived credential in the system, and it grates. GHCR does not accept GitHub App tokens for image pulls. If you'd rather avoid it: make the demo package public after the first push (Package settings → Change visibility), set `registry_username` to `""` in the tfvars, and skip this step entirely. For a POC that is a defensible trade.

---

## Step 4 — Repository configuration (~10 min)

Paste the `gh variable set` commands from Step 1, then add the secrets:

```bash
OWNER=main0034

# GitHub downloads the key as aaas-bot.YYYY-MM-DD.private-key.pem - the date
# is part of the filename, so resolve it rather than guessing.
PEM=$(ls -t ~/Downloads/*private-key.pem | head -1)
echo "Using key: $PEM"     # sanity-check this is the right file before continuing

for repo in aaas-deployments aaas-app-demo; do
  gh secret set AAAS_APP_ID          --repo $OWNER/$repo --body "<app-id>"
  gh secret set AAAS_APP_PRIVATE_KEY --repo $OWNER/$repo < "$PEM"
done

# GHCR pull credential (skip if you made the package public)
gh secret set GHCR_READ_TOKEN --repo $OWNER/aaas-deployments --body "<ghcr-pat>"
```

Then confirm all three landed. `gh secret list` shows names, never values:

```bash
gh secret list --repo $OWNER/aaas-deployments
gh secret list --repo $OWNER/aaas-app-demo
```

Both of this step's likely mistakes surface much later, in the `plan` job's "Mint GitHub App token" step, with errors that do not point back here:

| Error | Cause |
|---|---|
| `Input required and not supplied: private-key` | `AAAS_APP_PRIVATE_KEY` not set — usually the `.pem` redirect failed |
| `'Issuer' claim ('iss') must be an Integer` | `AAAS_APP_ID` holds the Client ID (`Iv23li…`) instead of the numeric App ID |
| `Not Found` on `/repos/.../installation` | The App is not installed on that repository |

If you have lost the `.pem`, generate a fresh one from the App's General page and revoke the old. The original download cannot be retrieved.

Create the `dev` environment on the deployments repo — `apply.yml` and `destroy.yml` both reference it:

```bash
gh api -X PUT repos/$OWNER/aaas-deployments/environments/dev
```

Leave it without required reviewers for now. Adding one later is a click, and no workflow changes.

---

## Step 5 — Push (~5 min)

Order matters: the modules repo must be tagged before anything references `?ref=v0.1.0`.

```bash
cd aaas-infra-modules
git add -A && git commit -m "feat: app-stack module and bootstrap"
git push -u origin master
git tag v0.1.0 && git push origin v0.1.0

cd ../aaas-deployments
git add -A && git commit -m "feat: pipeline, schema, demo deployment"
git push -u origin master

cd ../aaas-app-template
git add -A && git commit -m "feat: FastAPI template with CI and release"
git push -u origin master

cd ../aaas-app-demo
git add -A && git commit -m "feat: demo app from template"
git push -u origin master
```

Then mark `aaas-app-template` as a template repository: **Settings → General → Template repository**.

**Expect `validate.yml` on the modules repo to fail on this first push.** I had no Terraform binary and no network in my sandbox, so nothing has been through `terraform validate` yet. Typical first-run failures are `terraform fmt` whitespace and an argument name that moved between provider versions. Paste the error at me and I'll fix it — then re-tag `v0.1.0` (`git tag -f v0.1.0 && git push -f origin v0.1.0`).

---

## Step 6 — Branch protection (~5 min)

Your instinct was right: this is a github.com step, not a CLI one. But first check whether it's available to you at all.

```bash
gh api user --jq .plan.name
```

**On GitHub Free, private repositories support neither classic branch protection nor rulesets.** Both are public-repo-only on that plan. If that command prints `free`, there is no setting to find — the UI genuinely does not offer it. Pick one of the three options below.

### If you have Pro / Team (or upgrade — ~$4/month)

**Settings → Branches → Add branch protection rule** on `aaas-deployments`:

- **Branch name pattern:** `master`
- ☑ Require a pull request before merging
- ☑ Require review from Code Owners
- ☑ Require status checks to pass before merging → search for and add **`gate`**
- ☑ Do not allow bypassing the above settings

Or as a single command:

```bash
gh api -X PUT repos/main0034/aaas-deployments/branches/master/protection \
  --input - <<'JSON'
{
  "required_status_checks": { "strict": true, "contexts": ["gate"] },
  "required_pull_request_reviews": { "require_code_owner_reviews": true, "required_approving_review_count": 1 },
  "enforce_admins": true,
  "restrictions": null
}
JSON
```

Require **`gate`**, not `validate` or `plan`. Those two are skipped on PRs that touch no deployments, and a required check that never reports leaves the PR stuck on "Waiting for status to be reported" indefinitely. `gate` always runs and always reports.

Note that `required_approving_review_count: 1` means you must approve your own agent's PRs — which is the point, but it does mean an extra click on every deploy.

### If you stay on Free — option A: make the repos public

`aaas-deployments` contains **no credentials by design**. Azure auth is OIDC, so there is nothing but subscription and client IDs, and those are identifiers rather than secrets. The GitHub App key and GHCR token live in Actions secrets, not in the repo.

Making `aaas-deployments` and `aaas-infra-modules` public unlocks branch protection for free, and also stops Actions minutes counting against your quota. What you give up is that your infrastructure design is visible. For a POC, that is usually a fine trade.

`aaas-app-demo` can stay private, but then its GHCR package stays private too, and you keep needing `GHCR_READ_TOKEN`.

### If you stay on Free — option B: accept CI-level guardrails

I have added a **`guardrails`** job to `plan.yml` that fails any PR touching `schemas/`, `scripts/`, `.github/`, or `agent/`. It enforces the same intent as CODEOWNERS, as a visible failing check rather than a merge block.

Be clear-eyed about what this is worth. It makes guardrail edits *loud* — you will see a red check — but it does not make them *impossible*, because nothing stops you (or an agent with your credentials) from merging a red PR. It is a smoke alarm, not a lock.

For a solo POC where you review every merge yourself, that is a reasonable position. It stops being reasonable the moment the agent runs unattended, which is Phase 5 at the earliest.

**My recommendation:** option A for the two infra repos. It costs nothing, gives you real enforcement, and the repos genuinely hold no secrets.

---

## Step 7 — Prove the pipeline, no agent (Phase 2)

The `demo` deployment already exists in the repo with `container_image` set to `:bootstrap`, so this is the first real test.

```bash
cd aaas-deployments
pip install jsonschema
python3 scripts/validate_deployment.py --all      # should pass locally first

# Workflow and guardrail changes go to master FIRST, on their own.
# The guardrails job fails any PR touching .github/, schemas/, scripts/ or
# agent/ - including a PR that is only trying to install the guardrails job.
# Push those to master directly, then branch.
git checkout master && git pull

git checkout -b test/first-deployment

# A REAL change under deployments/. An empty commit will not do:
# the detect job diffs the changed files, and an empty commit changes none,
# so there is nothing to plan.
jq '.app_env.LOG_LEVEL = "debug"' deployments/dev/demo/terraform.tfvars.json > tmp \
  && mv tmp deployments/dev/demo/terraform.tfvars.json

git commit -am "test: trigger plan for demo"
git push -u origin test/first-deployment
gh pr create --fill
```

Watch for, in order: `guardrails` passes, schema validation passes in seconds, then a plan comment appears with ~15 resources to add, then `gate` goes green.

If you opened a PR earlier and saw no checks at all, that was the empty-commit problem — `plan.yml` used to be filtered to `paths: deployments/**`, and an empty commit matches no path. The workflow now runs on every PR and decides internally, so checks always report. Close that old PR and start again with the command above.

**Don't merge yet** — `:bootstrap` isn't a real image. Get the plan green, then go to Step 8 and let the app repo produce a real tag first.

---

## Step 8 — First real deployment

**Build the image before the first apply.** A new deployment starts with `container_image` set to `:bootstrap`, which does not exist. Applying that creates the whole stack — including a Postgres server that takes 10–15 minutes — and then leaves the container app unable to pull. Debugging that on a first apply is miserable, because you cannot tell a networking problem from a missing image.

1. **Push `aaas-app-demo` to `master`.** `ci.yml` lints, tests, builds and smoke-tests the container. `release.yml` then pushes `ghcr.io/main0034/aaas-app-demo:<sha>` and opens a bump PR against `aaas-deployments`.

2. **Sort out image pull access.** The GHCR package is private by default:
   - private → `GHCR_READ_TOKEN` must be set on `aaas-deployments`
   - public → Package settings → Change visibility, then set `registry_username` to `""` in the tfvars

3. **Take the real SHA into the deployment PR**, alongside any module-ref change, and merge **once**. One apply, valid image.

4. **First apply takes 10–15 minutes**, almost all of it Postgres provisioning. The workflow comments the app URL when it finishes.

5. **Verify both endpoints:**
   ```bash
   URL=$(gh run view --repo main0034/aaas-deployments --json jobs -q '.' >/dev/null; echo)
   # or take the URL from the apply job's PR comment / workflow log

   curl -s $URL/health            # {"status":"ok","app":"demo"}
   curl -s $URL/ready             # {"database":"ok"}   <- the meaningful one
   ```

   `/health` passing only proves the container started. **`/ready` returning `{"database":"ok"}` is the actual acceptance test** — it proves the delegated subnet, the private DNS zone, the Key Vault reference and the managed identity all work together. That combination is the part of the module most likely to be subtly wrong, and the part that has never been exercised.

   If `/ready` says `unavailable`, the likely cause is the private DNS zone link, and the app's logs will show a connection timeout rather than an auth error:
   ```bash
   az containerapp logs show -n ca-demo-dev -g rg-demo-dev --tail 50
   ```

   Scale-to-zero means the first request after idle takes several seconds. A slow first `curl` is expected, not a fault.

6. **Exercise the database properly** — a read alone can pass with an empty schema:
   ```bash
   curl -s -X POST $URL/items -H 'content-type: application/json' -d '{"title":"first"}'
   curl -s $URL/items
   ```
   A successful write and read-back proves the connection string, the schema init and the credential path end to end.

7. **Record the numbers while you have them.** These are Phase 6 data and are annoying to reconstruct later:
   - wall-clock of the apply job
   - resource count created
   - anything that behaved unexpectedly

   Put them in `FINDINGS.md`.

8. **Run `destroy.yml` the same day.** Proving teardown is part of this phase, not an afterthought — and it stops the meter.
   ```
   Actions → destroy → deployment: deployments/dev/demo, confirm: demo
   ```
   Expect 10–20 minutes, mostly Postgres. Then confirm nothing is left:
   ```bash
   az group list -o table | grep demo   # expect no rows
   ```
   A resource group that survives teardown is a finding worth writing down, not something to delete by hand and forget.

### Then prove the handoff (Phase 4b)

Change one visible thing in the demo app — the `/health` response, say — and merge. You should see: image built, bump PR opened automatically, its plan showing **exactly one changed attribute**, and a new revision live in about two minutes.

If that plan shows more than `container_image` changing, stop and investigate before merging. The whole point of the tfvars-as-data design is that a redeploy is a one-line diff.

---

## Step 9 — Tear down when you stop for the day

Actions → **destroy** → run with `deployments/dev/demo` and confirm `demo`.

### What this costs

One deployment left running continuously is **~$17–20/month**, and it is almost entirely Postgres:

| | Cost/month |
|---|---|
| Postgres compute (`B_Standard_B1ms`) | ~$12–15 |
| Postgres storage (32 GB) | ~$4 |
| Private DNS zone | ~$0.50 |
| Container app, environment, Log Analytics, Key Vault, VNet | ~$0 |

The container app is genuinely free at this size: the Container Apps monthly free grant covers about 200 hours at `cpu = 0.25`, and `min_replicas = 0` means nothing is billed while idle.

**Postgres bills per hour whether or not anything connects to it.** So the only lever that matters is destroying deployments you aren't using — shrinking the container saves nothing, because it already costs nothing.

Allowed SKUs are capped at `B_Standard_B2s` and 64 GB storage, so no single deployment can get expensive. The realistic overspend is several deployments left running during Phase 4/5 iteration, which is what the $20 budget alert is calibrated to catch.

Destroy takes 10–20 minutes, mostly waiting for Postgres.

---

## What I still owe you

- Fixes to whatever `terraform validate` rejects on first push
- `agent/create-app.md` — the app-repo runbook (deliberately left until the pipeline is proven)
- The agent's own harness: which SDK, what tool restrictions, how it's invoked

That last one is Phase 4a and shouldn't start until Step 8 is green.
