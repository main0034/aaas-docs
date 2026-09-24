# Next session

Written 24 September 2026 at the end of the D-22 readiness-gate session. Replaced wholesale at
the end of every session — this file is intent, not history. What actually happened lives in
`FINDINGS.md`, where things stand lives in `STATUS.md`.

---

## Topic

**Phase 4c — make the agent able to write the application.** A .NET SDK in the harness image,
a policy that allows `dotnet`, a token that can reach an app repo, and one run of
`agent/create-app.md` end to end, timed.

One topic. If something else turns out to block it, say which and why before widening.

## Why this one

The deploy loop is now honest: a green pipeline means the app runs (D-22, finding 19). The
biggest unknown left in the POC is still the one in finding 14 — **prompt-to-running-app time**
— and it cannot be measured while `create-app.md` is a runbook the agent is physically unable
to follow. Every `dotnet` command in it would be refused by the policy, and the image has no SDK.
Phase 5 (fix-forward) and Phase 7 (code generation) both sit on top of this.

## Read first, in this order

| File | Why |
|---|---|
| `aaas-deployments/agent/create-app.md` | The runbook this session makes runnable. Its header says what is missing. |
| `aaas-agent/harness/policy.py` | `ALLOWED_COMMANDS`, and the `python3` reasoning at the top — `dotnet` is the same problem, larger. |
| `aaas-agent/Dockerfile`, `aaas-agent/scripts/verify-isolation` | What the image deliberately lacks, and what is asserted at start. |
| `FINDINGS.md` findings 14 and 19 | 14: what the first agent run cost and where. 19, point 5: token permissions are a boundary GitHub enforces. |
| `aaas-app-template/AGENT.md` | The conventions the agent has to write within. |

## State to verify before starting

Don't trust this file on any of these — check:

- **Is infrastructure destroyed?** It was destroyed on 24 September. Check the latest `destroy`
  run and, if you can, `az group exists -n rg-demo-dev` - a green destroy has lied before
  (finding 20). This session's end-to-end run will recreate the stack (~9 minutes).
- Is everything pushed? `aaas-deployments` had one commit left locally at the end of the last
  session (a comment in `destroy.yml`).
- Is `aaas-app-demo` local checkout still on the stale `test/unique-title` branch with an
  uncommitted `http/items.http` edit? Origin's `master` is `43d8be2`.
- Does `aaas-agent` build and pass its tests as it stands, before anything is changed?
- Was last session's GitHub token revoked? (The file is gone.)

## The work

1. **SDK in the image.** Add the .NET 10 SDK to `aaas-agent/Dockerfile`, matching the
   template's `global.json`. Keep `verify-isolation` asserting that `az`/`terraform` are absent.
2. **Policy for `dotnet`.** Allow the subcommands the runbook names (`build`, `test`, `format`,
   `ef migrations add`, `restore`) and refuse the rest (`tool install`, `new` outside the
   template, `nuget add source`). Say in the code, as for `python3`, that this is not the
   boundary: `dotnet build` and `dotnet test` execute arbitrary code by design.
3. **Token for the app repo.** The agent's `GH_TOKEN` is scoped to `aaas-deployments`.
   `create-app.md` pushes to an application repo. Widen it to exactly one app repo, with
   Contents + Pull requests and **no Workflows permission** (finding 19).
4. **Run it once**, against `aaas-app-demo` with a small change, and time it: prompt → app PR →
   merge → image → deployment PR → merge → readiness gate green. That is the number finding 14
   left open.
5. **Read the refusals** in `runs/<id>/report.md` and fix the runbook where a refusal recurs.

## Decisions I will need from you

- **What contains `dotnet`.** Once the agent can run `dotnet test`, it can run any code it
  writes, with `GH_TOKEN` in the environment and network egress for NuGet. The real boundary
  is then (a) no Azure credential in the container, which holds, and (b) what `GH_TOKEN` can
  do. Expect me to argue that (b) is enough for the POC and that an egress allowlist is a
  product-time concern — but it is your call.
- **Which app repo the run targets.** `aaas-app-demo` (existing, has history, will produce a
  deployment PR the gate may turn red because of `43d8be2` — see STATUS) or a fresh repo from
  the template (tests the onboarding surface, OQ-15, which is out of scope).
- **Whether 4c is one job or two runs.** POC-PLAN says one job chaining both runbooks. Two
  separate runs is simpler and gives the same timing data. Expect me to propose two runs first.

## Done when

- The harness image has a .NET SDK and `verify-isolation` still passes.
- The policy allows the runbook's `dotnet` commands and refuses the rest, with tests.
- One agent run produces a merged app PR, and the chain reaches a green readiness gate.
- Prompt-to-running-app time is written into `FINDINGS.md`, with the cost per run.

## Explicitly not this session

- OQ-21 (immutable once *applied*) — real, but revert works as recovery today.
- The post-destroy assertion (finding 20) — small, but its own piece of work.
- OQ-15 (automated repo onboarding), anything commercial (D-19), revisiting Azure or .NET.

## Carried over

- **OQ-21 — "immutable once merged" is the wrong rule.** Unchanged by the gate: it made the
  failure visible, not fixable forward.
- **A green destroy is not evidence the stack is gone (finding 20).** Add `az group exists` must
  be `false` after `terraform destroy`. The child-resource hang is fixed, but a full destroy
  still took 25 minutes - close to where earlier runs died on OIDC renewal.
- **`gh pr close` is refused by the policy** while `PROMPT.md` only forbids merging.
- **`.terraform.lock.hcl` does not exist.** Provider versions can drift between runs.
- **Log Analytics revision filter returned another run's output** (finding 19, point 2).
  Unexplained; the gate works around it.
- **`verify-isolation` cannot see token permissions.** Asserting "no Workflows permission" on
  `GH_TOKEN` would make finding 19's boundary checked rather than assumed.
