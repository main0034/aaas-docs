# aaas-docs

The written record for AaaS: what it is, what has been built, what the build taught us, and
what the next session is for. No code lives here.

## The workspace

Every repository is a **sibling** of this one. Paths in these documents are written relative
to the workspace root — `aaas-deployments/agent/PROMPT.md` means the file at that path next
to this repository, not inside it.

```
~/Documents/Programmering/aaas/       <- workspace root, not a repository
├── aaas-docs/                        <- this repository
├── aaas-agent/                       harness: runs the agent in a container
├── aaas-deployments/                 the state of the world; one directory per deployment
├── aaas-infra-modules/               the app-stack Terraform module, versioned by tag
├── aaas-app-template/                GitHub template every generated app starts from
└── aaas-app-demo/                    the first application, built by hand
```

Clone all six as siblings and the references resolve. Clone this one alone and you can still
read everything; only the file paths point at nothing.

## Read in this order

| File | What it is |
|---|---|
| `NEXT-SESSION.md` | What the next session is for, and what to verify before starting. **Start here.** Replaced wholesale each session — it is intent, not history. |
| `STATUS.md` | Where the build stands. What is proven, what is not, what is uncommitted. |
| `FINDINGS.md` | What the build actually taught us. The most valuable document here. |
| `AaaS-context.md` | The product: decisions, rationale, open questions. Product level, not build level. |
| `AaaS-instructions.md` | How to work on this with Claude. Read alongside the context document. |
| `POC-PLAN.md` | The original plan and phase structure. Historical — the build has overtaken parts of it. |
| `SETUP.md` | Runbook for bootstrap, secrets, and the deploy/destroy loop. |

## Why these are not in aaas-deployments

Two reasons, and the second is the practical one:

1. They are cross-cutting. They describe the estate, not any one repository in it.
2. **The agent clones `aaas-deployments` on every run.** Anything added there becomes part of
   the agent's working environment, and finding 14 measured that context — not output — is
   what agent runs cost. Product strategy, competitive analysis and a findings log are
   exactly the kind of thing that should never end up in an agent's context window.

## Private, deliberately

The other repositories were made public to get branch protection on the GitHub Free plan
(finding 6). Nothing gates this one — no CI, no CODEOWNERS, no required checks — so privacy
costs nothing here. It also contains competitive analysis, cost figures and commercial
thinking, which is reason enough on its own.

## A note on two of these files

`AaaS-context.md` and `AaaS-instructions.md` also exist as documents in the Claude project.
The project copy is what Claude reads by default; the copy here is the versioned record.
They are kept in step — an edit to one is an edit to both. If they ever disagree, the
project copy is the one that was being used, and this one is the one with the history.
