# AI Architecture

> **Implementation status: NOTHING IN THIS DOCUMENT IS IMPLEMENTED YET.**
>
> No AI provider, abstraction, orchestrator, router or feature exists in the repository. There
> are configuration placeholders under `Ai:*` in `appsettings.json` and nothing reads them.
> Everything below marked *planned* is design intent for Phases 3, 5 and 7.
>
> Sections marked **[implemented]** describe foundation pieces that already exist and that the
> AI layer will build on.

---

## 1. Rule (planned, binding when built)

Domain and Application code never names a provider. No `OpenAI`, `Anthropic`, `Ollama`, or
`OpenClaw` type, string, or model id appears outside the AI Infrastructure layer.

```
Feature handler
   → IAiCompletion / IAiAnalysis / IAiAgent      (Shared.Application abstractions)
   → AI orchestrator: authorize → build context → select provider → execute → record
   → provider adapters                            (Shared.Infrastructure / <Context>.AI.Infrastructure)
        Ollama · OpenAI · Anthropic · OpenClaw
```

## 2. Provider strategy (planned)

| Provider | Role | Typical work |
| --- | --- | --- |
| **Ollama** | Cost-effective baseline; powers Free and lower tiers | Basic Q&A, simple summaries, basic classification, basic document analysis |
| **OpenAI** | Advanced general workloads | Advanced analysis, document understanding, drafting, more complex assistance |
| **Anthropic** | Complex reasoning, large context | Complex audit analysis, complex financial reasoning, large-context document analysis |
| **OpenClaw** | Agentic execution | Multi-step workflows, tool orchestration |

Selection inputs, in order: **authorization → entitlement (`ai_max_tier`, remaining
`ai_monthly_units`, `ai_max_context_tokens`) → task capability → complexity → context size → cost.**

Never default to the most expensive model. A request that exceeds the organization's tier is
**downgraded to the best permitted provider or refused with an upgrade-relevant message** — never
silently escalated.

### Entitlement keys **[implemented]**

The keys and the tier ordering already exist and are unit-tested:

- `Ledgance.Shared.Application.Subscriptions.Entitlements` — `AiEnabled`, `AiMonthlyUnits`,
  `AiMaxTier`, `AiMaxContextTokens`.
- `AiTiers` — `basic < advanced < reasoning < agentic`, compared with `AiTiers.Allows`.
- `IEntitlementService` resolves them per organization and module.

The router will read these; it must not define its own plan logic.

## 3. Layering (planned)

- `Ledgance.Shared.Application` — provider-agnostic AI abstractions, request/response contracts,
  routing policy interface, usage-accounting interface.
- `Ledgance.Shared.Infrastructure` — provider adapters, HTTP clients, credentials from
  configuration, retry/timeout, token accounting.
- `Modules/<Context>/AI/*.Application` — product-specific AI features as Mediator commands and
  queries, expressed in domain language ("summarise this working paper"), not prompt language.
- `Modules/<Context>/AI/*.Domain` — AI concepts that carry domain meaning (a risk suggestion, a
  review observation) as first-class typed results.
- `Modules/<Context>/AI/*.Infrastructure` — context assembly: turning authorized engagement or
  period data into model input.

The four `*.AI.*` projects exist and are correctly referenced **[implemented]**, but contain no
code.

Audit AI and Accounting AI are separate slices. Neither reaches into the other's data.

## 4. Context and authorization (planned)

Every AI call resolves its context through the **same** authorization path as a normal query.

1. Resolve the user, organization, role and — for Audit — engagement assignment.
2. Assemble context **only** from records that user could already read directly.
3. Enforce entitlements on tier, usage and context size.
4. Execute.
5. Record usage against the organization's allowance and write to the activity trail.

There is no privileged AI service account, no service-role path used to widen AI context, and no
cross-organization context. AI is a *reader* subject to the same rules as its caller.

The pieces this depends on — `ICurrentUserAccessor`, `AuthorizationBehavior`,
`EntitlementBehavior`, `SupabaseRepository` tenant scoping — are **[implemented]**.

## 5. Output handling (planned)

AI output is a **proposal**. It is stored and presented as AI-generated, attributed and
reviewable. A human accepts it before it becomes part of the audit or accounting record, and the
acceptance is recorded with the accepting user.

Applies to: risk suggestions, working-paper drafts, finding drafts, report drafts, journal-entry
suggestions, categorisation suggestions, reconciliation matches, anomaly flags.

AI does not replace professional auditor or accountant judgment.

## 6. Audit AI scope (planned — Phase 3)

| Tier | Capabilities |
| --- | --- |
| Basic | Audit assistant · engagement Q&A · document summarization · basic evidence summarization · basic working-paper assistance |
| Intermediate | Risk suggestions · evidence analysis · working-paper drafting · finding drafting · audit procedure assistance · cross-document analysis |
| Advanced | Complex risk analysis · complex evidence analysis · anomaly detection · cross-document reasoning · review assistance · audit report drafting · complex engagement analysis |
| Agentic | Multi-step engagement analysis · authorized evidence investigation · cross-source analysis · automated preparation workflows · AI-assisted review workflows |

Constraint: AI may only access engagements the user is assigned to, and must respect
organization, client, engagement, user, role, permissions and subscription.

## 7. Accounting AI scope (planned — Phase 5)

| Tier | Capabilities |
| --- | --- |
| Basic | Accounting assistant · accounting Q&A · transaction explanations · basic categorization assistance · financial summaries |
| Intermediate | Journal-entry assistance · reconciliation assistance · financial statement explanation · variance analysis · document analysis |
| Advanced | Financial anomaly detection · complex variance analysis · cross-document reasoning · advanced financial analysis · complex accounting explanations |
| Agentic | Multi-step accounting analysis · authorized reconciliation workflows · financial investigation · automated preparation assistance · AI-assisted accounting workflows |

Constraint: AI may only access entities and periods the user is authorized for, and must never
bypass Accounting business logic.

## 8. Agentic AI — OpenClaw (planned — Phase 7)

The highest tier and the most tightly constrained.

- The agent's tool set is a **whitelist of Mediator requests**, each executed through the normal
  authorization and entitlement pipeline. **Agents never touch the database directly.**
- Every step is bounded (step count, wall clock, token budget) and fully logged.
- Any material change proposed by an agent still requires human acceptance.

## 9. Configuration

Endpoints, model ids, per-tier routing policy and usage caps live in configuration. API keys come
from `appsettings.local.json` or environment variables and are **never** exposed to the frontend.
Committed files carry placeholders only.

Present in `appsettings.json` as placeholders **[implemented]**, unread by any code:

```
Ai:Ollama:BaseUrl
Ai:OpenAI:ApiKey
Ai:Anthropic:ApiKey
Ai:OpenClaw:BaseUrl
Ai:OpenClaw:ApiKey
```

Still to be designed: `Ai:Routing:*` (tier → provider/model policy) and usage-accounting storage.
