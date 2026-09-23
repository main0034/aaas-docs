# AaaS — working instructions for Claude

Read `AaaS-context.md` before responding to anything about the AaaS product. It is the
source of truth for decisions already made.

**Then read, in `aaas-docs`** (`~/Documents/Programmering/aaas/aaas-docs`): `NEXT-SESSION.md` for
what this session is for, `STATUS.md` for where the build actually stands, and `FINDINGS.md`
for what it taught us.

If I say *"continue with the Next session instructions"*, that means `NEXT-SESSION.md` — read
it, verify the state it tells you to verify, and start. Do not re-plan what it already
decided. The context document is
product-level and goes stale between sessions; those two are current. Reading one without the
other is how the build and the plan diverged for five weeks.

## How to work with me on this

- **Don't re-litigate settled decisions.** Section 7 of the context document lists them.
  If you think one is wrong, say so once, with the reason, then move on.
- **Push back.** I would rather hear "this won't work because X" than get an agreeable
  plan I have to discover is wrong later. Name the risk, name the tradeoff.
- **One topic per session.** I'll say what it is. Don't sprawl into adjacent open questions
  unless they genuinely block the topic — if they do, say which and why.
- **Be concise.** No preamble, no recap of what I just said, no summary of the summary.
- **Concrete over abstract.** Prefer a named Azure service, a real Terraform resource, a
  specific number, over "consider your requirements".

## Maintaining the context document

At the end of a session where something was settled:

1. Add a row to §7 Decisions with date and one-line rationale.
2. Remove the corresponding OQ entry from §8.
3. Add any new open questions the discussion exposed.
4. Update the version and date in the header.

Ask before rewriting a whole section. Small edits, just do them and tell me what changed.

## Standing constraints

These are load-bearing. If a proposal violates one, flag it explicitly.

- Terraform for all infrastructure, on every platform. Non-negotiable.
- **Azure is the v1 platform (D-13).** Residency, not sovereignty. Vercel/Supabase remain
  ruled out (D-10). Assume Azure; a platform change is a deliberate decision to reopen, not a
  hedge to carry through every proposal.
- Applications run in **our** tenancy, **one isolated tenancy per customer**. Never
  co-mingle two customers — it breaks cost splitting, blast radius and the handover path
  all at once.
- Residency is the v1 claim: data stays in `swedencentral`. Sovereignty is deferred, not
  abandoned — if the ICP turns out to need it (OQ-3c), D-13 gets revisited and that is a
  rebuild, so flag anything that would make a later move materially harder.
- **Manage zero secrets (D-14).** Anything that introduces a credential Terraform has to store
  is a regression, however well it is stored. Say so.
- **Guardrails are deterministic (D-17).** Prefer a schema enum, a Terraform `validation` block
  or a CI check over asking a model to review. An agent that can widen its own constraints has
  none.
- Don't propose building our own code-generation engine (D-9).
- The end user is non-technical and never sees git, Terraform, or the Azure portal.
- Git is the source of truth. Agents open pull requests; **only the pipeline holds
  credentials to customer Azure**. No agent gets a subscription-scoped secret.
- Standardization beats flexibility. A request that doesn't fit an archetype is either
  refused or becomes a new archetype deliberately — never a one-off.
- v1 is one golden path, finished, before any second archetype.

## Next session

Deliberately not recorded here. `NEXT-SESSION.md` in `aaas-docs` is the only place the next
topic lives, and it is rewritten at the end of every session. A second copy in this file went
stale within days and contradicted it — the same drift this setup exists to prevent.

## Ending a session

When I say we are stopping, or the topic is done, do all of this without being asked:

1. **Rewrite `NEXT-SESSION.md` wholesale** for the next topic. It is intent, not history —
   replace it, do not append. Keep the shape it already has: topic, why, what to read, state
   to verify, the work, decisions needed from me, done-when, what is out of scope, carried
   over. Say what I will be asked to decide, so I arrive ready.
2. **Update `STATUS.md`** — what is now proven, what the next step is.
3. **Add to `FINDINGS.md`** if the session taught something a future session would otherwise
   rediscover. Not a diary; only things that cost time or changed a decision.
4. **Update `AaaS-context.md`** per the rules above if anything was settled.
5. **Tell me what is uncommitted or unpushed**, per repository, and which of it matters.

The point of 1 is that I should be able to pick this up after two weeks without reloading it
all from memory, and that you should not have to guess what we were in the middle of.
