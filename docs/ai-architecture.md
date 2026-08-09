# AI Architecture

## 1. Rule

Domain and Application code never names a provider. No `OpenAI`, `Anthropic`, `Ollama`, or
`OpenClaw` type, string, or model id appears outside the AI Infrastructure layer.

```
Feature handler
   → IAiCompletion / IAiAnalysis / IAiAgent      (Shared.Application abstractions)
   → AI orchestrator: authorize → build context → select provider → execute → record
   → provider adapters                            (Shared.Infrastructure / <Context>.AI.Infrastructure)
        Ollama · OpenAI · Anthropic · OpenClaw
```

## 2. Provider strategy

| Provider | Role | Typical work |
| --- | --- | --- |
| **Ollama** | Cost-effective baseline; powers Free and lower tiers | Summaries, classification, simple Q&A, basic document analysis |
| **OpenAI** | Advanced general workloads | Document understanding, analysis, drafting, advanced assistance |
| **Anthropic** | Complex reasoning, large context | Deep audit/accounting analysis, cross-document reasoning |
| **OpenClaw** | Agentic execution | Tool orchestration, multi-step workflows |

Selection inputs, in order: **authorization → entitlement (`ai_max_tier`, remaining
`ai_monthly_units`, `ai_max_context_tokens`) → task capability → complexity → context size → cost.**

Those keys already exist in `Ledgance.Shared.Application.Subscriptions.Entitlements`, and the
tier ordering (`basic < advanced < reasoning < agentic`) in `AiTiers.Allows`. The router reads
them through `IEntitlementService`; it does not define its own plan logic.

A request that exceeds the organisation's tier is **downgraded to the best permitted provider or
refused with an upgrade-relevant message** — never silently escalated.

## 3. Layering

- `Ledgance.Shared.Application` — provider-agnostic AI abstractions, request/response contracts,
  routing policy interface, usage-accounting interface.
- `Ledgance.Shared.Infrastructure` — provider adapters, HTTP clients, credentials from
  configuration, retry/timeout, token accounting.
- `Modules/<Context>/AI/*.Application` — product-specific AI features as Mediator commands and
  queries, expressed in domain language ("summarise this working paper"), not in prompt language.
- `Modules/<Context>/AI/*.Domain` — AI concepts that carry domain meaning (e.g. a risk
  suggestion, a review observation) as first-class typed results.
- `Modules/<Context>/AI/*.Infrastructure` — context assembly: turning authorised engagement or
  period data into model input.

Audit AI and Accounting AI are separate slices. Neither reaches into the other's data.

## 4. Context and authorization

Every AI call resolves its context through the **same** authorization path as a normal query.

1. Resolve the user, organisation, role, and — for Audit — engagement assignment.
2. Assemble context **only** from records that user could already read directly.
3. Enforce entitlements on tier, usage, and context size.
4. Execute.
5. Record usage against the organisation's allowance and write to the activity trail.

There is no privileged AI service account, no service-role key path used to widen AI context,
and no cross-organisation context. AI is a *reader* subject to the same rules as its caller.

## 5. Output handling

AI output is a **proposal**. It is stored and presented as AI-generated, attributed, and
reviewable. A human accepts it before it becomes part of the audit or accounting record, and
the acceptance is recorded with the accepting user.

Applies to: risk suggestions, working-paper drafts, finding drafts, report drafts, journal-entry
suggestions, categorisation suggestions, reconciliation matches, anomaly flags.

## 6. Audit AI scope

Audit assistant · engagement Q&A · document summarisation · risk suggestion and analysis ·
evidence analysis · working-paper drafting · finding drafting · audit report drafting ·
anomaly detection · cross-document reasoning · AI-assisted review · audit intelligence.

Constraint: AI may only access the engagements the user is assigned to.

## 7. Accounting AI scope

Accounting assistant · accounting Q&A · transaction explanation · categorisation assistance ·
journal-entry assistance · reconciliation assistance · financial summaries · financial
statement explanation · variance analysis · anomaly detection · document analysis ·
accounting-context analysis.

Constraint: AI may only access entities and periods the user is authorised for.

## 8. Agentic AI (OpenClaw)

Agentic workflows are the highest tier and the most tightly constrained.

- The agent's tool set is a **whitelist** of Mediator requests, each executed through the normal
  authorization and entitlement pipeline. There is no direct data access.
- Every step is bounded (step count, wall clock, token budget) and fully logged.
- Any material change proposed by an agent still requires human acceptance.

## 9. Configuration

Endpoints, model ids, per-tier routing policy, and usage caps live in configuration.
API keys and credentials come from `appsettings.local.json` or environment variables and are
**never** exposed to the frontend. Committed files carry placeholders only.

Anticipated keys:

```
Ai:Ollama:BaseUrl
Ai:OpenAI:ApiKey
Ai:Anthropic:ApiKey
Ai:OpenClaw:*
Ai:Routing:*        (tier → provider/model policy)
```
