# Next session

Written 22 September 2026 at the end of the OQ-13 / .NET session. Replaced wholesale at the end
of every session — this file is intent, not history. What actually happened lives in
`FINDINGS.md`, where things stand lives in `STATUS.md`.

---

## Topic

**D-22 — the post-apply readiness gate.** Make a green deploy mean the application actually
runs.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

Finding 16 is the whole argument, and it is not a hypothetical: a migration that fails only
against live data produced

- `terraform apply` → **success**
- revision `--0000002` → **`ActivationFailed`**, holding **100% of the traffic**
- the previous revision still serving every request
- the first visitor waiting **42.8s** to reach an app running the old code

At `min_replicas = 0` nothing starts during the apply, so nothing fails. Every downstream
promise — fix-forward (Phase 5), rollback as one button (§2), day-2 operations (§6 Operator) —
assumes the pipeline knows whether a deploy worked. Right now it does not.

## Read first, in this order

| File | Why |
|---|---|
| `FINDINGS.md` finding 16 | The evidence, with the exact states and timings. |
| `FINDINGS.md` finding 17 | `app_url` is produced and discarded; the gate needs it anyway. |
| `STATUS.md` | Current position; infrastructure is destroyed. |
| `aaas-deployments/.github/workflows/apply.yml` | Where the gate goes. |
| `aaas-infra-modules/modules/app-stack/README.md` | "Known behaviours" now records what a failed migration looks like. |

## State to verify before starting

Don't trust this file on any of these — check:

- Is infrastructure still destroyed? Nothing should be costing money.
- Are `aaas-docs` and the `app-stack` README change pushed?
- Does `aaas-deployments/master` contain `agent/create-app.md`?
- Is `app-stack` still at `v0.3.0`, and does `deployments/dev/demo/main.tf` pin it?

## The work

1. **Decide what the gate asserts.** The minimum that would have caught finding 16: force a
   replica (one request to `/health`, or set `min_replicas = 1` for the duration), then poll
   `/ready` until it reports the migration this release expects — not merely that it answers.
   The expected migration ID has to come from somewhere; the image knows it, the pipeline does
   not. That is the design question.
2. **Decide where it runs.** A step in `apply.yml` after `terraform apply` is the obvious place,
   and it needs `app_url` from the Terraform output — which also fixes finding 17.
3. **Decide what happens when it fails.** The apply already succeeded and the revision is
   broken. Options: fail the workflow and leave it (the old revision is still serving, so the
   customer is not down), or attempt an automatic rollback to the previous image. Rollback is a
   second deploy and can fail the same way. Relevant to OQ-5.
4. **Surface the reason.** On failure, fetch the init container's log from Log Analytics
   (`ContainerAppConsoleLogs_CL | where ContainerName_s == "migrate"`) and put the
   `[migrate] FAILED:` line in the workflow summary or on the commit. `az containerapp logs
   show --container migrate` does not work — it answers `Could not find container`.
5. **Prove it** with the same test that produced finding 16: two items sharing a title, then the
   `UniqueItemTitle` migration. The branch is gone but the recipe is in finding 16. The gate
   must go red.

## Decisions I will need from you

- **How the pipeline learns the expected migration ID.** Candidates: the image writes it to a
  file the release reads, `release.yml` extracts it from the built image, or `/ready` grows a
  "pending migrations" count and the gate asserts zero. The third is the smallest and needs no
  new plumbing — expect me to argue for it.
- **Fail-and-stop versus automatic rollback** when the gate goes red (step 3).
- Whether the gate also runs on the *first* apply, where there is no previous revision to fall
  back to and a failure means the app has never worked.

## Done when

- A deploy whose migration fails against live data turns the pipeline red.
- The failure names the cause, from the init container's own log.
- `app_url` is visible without opening the Actions log or the portal.
- `AaaS-context.md` §7 has the outcome and D-22 is either satisfied or amended.

## Explicitly not this session

- Phase 4c and the harness's missing .NET SDK. It is next, not now.
- Anything commercial — parked under D-19.
- Revisiting Azure, or the .NET decision (D-20). Closed.

## Carried over

- **OQ-21 — "immutable once merged" is the wrong rule.** The guard blocks fixing forward a
  migration that was merged but never applied, which is exactly the state finding 16 produced.
  Needs the pipeline to know what each estate has applied (same missing fact as OQ-16).
- **Phase 4c needs a .NET SDK in the harness image** and a policy that allows `dotnet`.
  `create-app.md` is committed and says so at the top.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **Offer to turn this hand-off into a skill**, so preparing it does not depend on remembering.
