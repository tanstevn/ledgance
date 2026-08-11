# AI Architecture

> **Implementation status:** the provider abstraction, routing, entitlement enforcement, usage
> accounting, the Audit AI capabilities (Phase 3), the Accounting AI capabilities (Phase 5)
> and the **agentic layer with OpenClaw** (Phase 7) are **implemented** — eleven capabilities
> per product including one agent each. No AI call has yet run against a live provider —
> everything is verified by unit tests against fakes.

---

## 1. Rule **[implemented]**

Domain and Application code never names a provider. No `OpenAI`, `Anthropic`, `Ollama`, or
`OpenClaw` type, string, or model id appears outside `Ledgance.Shared.Infrastructure/Ai`.

```
Feature handler (module Application)
   → IAiCompletionService.CompleteAsync(AiWorkload)          Shared.Application/Ai
   → AiCompletionService (orchestrator)                      Shared.Infrastructure/Ai
        authorize → entitlements (tier, units, context) → route → execute → record usage
   → IAiChatClient adapters:  Ollama · OpenAI · Anthropic    Shared.Infrastructure/Ai
```

An `AiWorkload` carries the module, a capability key, the **required tier**, prompts and
context documents. The orchestrator is the only path to a provider.

## 2. Provider strategy **[implemented]**

| Provider | Role | Adapter |
| --- | --- | --- |
| **Ollama** | Cost-effective baseline (`basic` tier) | Raw HTTP `api/chat` |
| **OpenAI** | Advanced general workloads (`advanced` tier) | Raw HTTP `v1/chat/completions` |
| **Anthropic** | Complex reasoning, large context (`reasoning` tier) | Official `Anthropic` C# SDK |
| **OpenClaw** | Agentic execution (`agentic` tier) | Raw HTTP `v1/agent/turns` (agent-turn protocol) |

Routing is tier → provider/model, declared in `ConfiguredAiModelRouter` defaults and
overridable per tier via `Ai:Routing:{tier}` configuration:

| Tier | Default route |
| --- | --- |
| `basic` | Ollama · `llama3.1:8b` |
| `advanced` | OpenAI · `gpt-4o` |
| `reasoning` | Anthropic · `claude-opus-5` |
| `agentic` | OpenClaw · `openclaw-agent-1` (falls back down the chain to Anthropic etc.) |

Selection order: **authorization → entitlement (`ai_max_tier`, remaining `ai_monthly_units`,
`ai_max_context_tokens`) → capability's required tier → cost**. A workload above the plan's
tier is refused with an upgrade-relevant `EntitlementException` (HTTP 402) — never silently
escalated. A provider failure falls back **down** the tier chain (never up); when every
provider fails, `AiUnavailableException` surfaces as HTTP 503. Requests never route to a more
expensive model than the capability requires.

## 3. Entitlement enforcement **[implemented]**

All server-side, inside the orchestrator (plus `[RequiresEntitlement(Audit, AiEnabled)]` on
every AI request for the pipeline gate):

1. `ai_enabled` capability check.
2. `ai_max_tier` vs the workload's required tier (`AiTiers.Allows`).
3. `ai_monthly_units`: usage read from the `ai_usage` table (one unit per completion, per
   organization + module + `yyyy-MM` period); exceeding it is a 402. Usage is recorded only on
   successful completions.
4. `ai_max_context_tokens`: context documents share the remaining token budget and are
   truncated per document (`[truncated]` marker); the assembled prompt is then checked against
   the limit (~4 chars/token estimate).

## 4. Context and authorization **[implemented]**

Every AI call resolves context through the same authorization path as a normal query:

- The mediator pipeline enforces authentication, permissions and the `AiEnabled` entitlement.
- Engagement-scoped capabilities call `IEngagementAccessGuard.EnsureMemberAsync` first — AI is
  confined to the caller's engagement team exactly like any other read.
- Context is assembled (`EngagementAiContext`) only from repositories the caller could query
  directly; there is no privileged AI data path and no service-role widening.
- Every engagement-scoped AI action writes to the activity trail (`ai.*` actions).

## 5. Output handling **[implemented]**

Every capability returns an `AiProposalResult` — capability key, content, provider, model,
tier, and a fixed disclaimer. **AI never writes to the audit record.** A human takes a proposal
into the record through the normal commands (add risk, save working paper, raise finding, save
report), which enforce their own authorization and domain rules and log their own activity.

## 6. Audit AI capabilities **[implemented — Phase 3]**

Catalog: `Ledgance.Audit.AI.Domain.AuditAiCapabilities` — the single place capability-to-tier
gating is declared. `GET /api/audit/ai/capabilities` reports each with an `included` flag for
the caller's plan.

| Tier required | Capability | Endpoint (`api/audit/ai/…`) |
| --- | --- | --- |
| basic | Assistant / engagement Q&A | `POST assistant` |
| basic | Document & evidence summarization | `POST engagements/{id}/summarize` |
| advanced | Risk suggestions | `POST engagements/{id}/suggest-risks` |
| advanced | Procedure suggestions | `POST engagements/{id}/suggest-procedures` |
| advanced | Working-paper drafting | `POST engagements/{id}/draft-working-paper` |
| advanced | Finding drafting | `POST engagements/{id}/draft-finding` |
| reasoning | Complex risk / cross-document analysis | `POST engagements/{id}/analyze-risks` |
| reasoning | Anomaly detection (trial balance) | `POST engagements/{id}/detect-anomalies` |
| reasoning | Review assistance | `POST engagements/{id}/assist-review` |
| reasoning | Audit report drafting | `POST engagements/{id}/draft-report` |
| agentic | Multi-step agentic investigation (§8) | `POST engagements/{id}/agent` |

Plan mapping (from `SubscriptionPlanCatalog`): Free → basic; Audit Professional → +advanced;
Audit Organization → +reasoning; Audit Firm/Enterprise → +agentic.

Known limits: evidence summarization works from recorded metadata/description (binary file
content extraction is future work); report drafting marks partner judgments with
`[PARTNER JUDGMENT]` and is gated to `Manage` + team membership.

## 7. Accounting AI capabilities **[implemented — Phase 5]**

Same orchestrator, same entitlements, `ProductModule.Accounting` workloads (metered
separately from Audit per organization). Catalog:
`Ledgance.Accounting.AI.Domain.AccountingAiCapabilities` — the single place
capability-to-tier gating is declared. `GET /api/accounting/ai/capabilities` reports each
with an `included` flag for the caller's plan.

| Tier required | Capability | Endpoint (`api/accounting/ai/…`) |
| --- | --- | --- |
| basic | Assistant / entity Q&A | `POST assistant` |
| basic | Journal-entry explanation | `POST entities/{id}/entries/{entryId}/explain` |
| basic | Period financial summary | `POST entities/{id}/periods/{periodId}/summarize` |
| advanced | Journal-entry suggestion | `POST entities/{id}/suggest-entry` |
| advanced | Reconciliation assistance | `POST entities/{id}/reconciliations/{recId}/assist` |
| advanced | Statement explanation | `POST entities/{id}/periods/{periodId}/explain-statements` |
| advanced | Variance analysis (two periods) | `POST entities/{id}/analyze-variance` |
| reasoning | Anomaly detection (ledger/TB) | `POST entities/{id}/periods/{periodId}/detect-anomalies` |
| reasoning | Complex financial analysis | `POST entities/{id}/periods/{periodId}/analyze-financials` |
| reasoning | Period-close review | `POST entities/{id}/periods/{periodId}/assist-close` |
| agentic | Multi-step agentic investigation (§8) | `POST entities/{id}/agent` |

Plan mapping (from `SubscriptionPlanCatalog`): Free → basic; Solo/Team → +advanced;
Accounting Professional → +reasoning; Accounting Enterprise → +agentic.

Context is assembled by `LedgerAiContext` from the Ledger module's own repositories (entity
overview, chart of accounts, journal entries, computed trial balance and statements,
reconciliation state) — the caller sees nothing through AI they could not query directly.
Every capability is entity-guarded, activity-logged (`ai.*` with the entity as context) and
returns an `AiProposalResult`; the close review additionally requires the
`accounting:ledger:manage` permission, mirroring who may actually close a period.

## 8. Agentic AI — OpenClaw **[implemented — Phase 7]**

```
User → agent command (module Application)
   → IAgentRunner.RunAsync(AgentWorkload)                  Shared.Application/Ai
   → AgentRunnerService                                    Shared.Infrastructure/Ai
        agentic tier gate → per-turn unit metering → route → loop:
           provider chooses a tool → tool = whitelisted mediator request
           → full pipeline (authorization, entitlements, validation) as the calling user
   → IAgentToolClient:  OpenClaw (native) · chat providers via ChatAgentAdapter (fallback)
```

The promise made in Phase 3 is now the mechanism: **an agent's tool set is a whitelist of
mediator requests executed through the normal pipeline.** Each tool call re-enters the
pipeline with the caller's identity, so a denial (permission, entitlement, team confinement,
domain rule) is contained as a tool result the agent must work around — never bypassed.
Tools are read-only queries; the run's answer is a proposal (`AgentRunReport` with the full
step transcript and a disclaimer). Agents never see a repository or the database, and the
engagement/entity id is fixed server-side — the agent cannot choose another scope.

Bounds: every provider turn consumes one usage unit and re-checks `ai_monthly_units`; tool
steps are capped (default 8) with a forced no-tools final turn; tool results are truncated.
OpenClaw only *chooses* tools over its `v1/agent/turns` protocol — execution never leaves
the application. If OpenClaw is unavailable, the run falls back down the provider chain,
driving the same loop over plain chat models through a strict-JSON protocol
(`ChatAgentAdapter`); an unparsable reply degrades to a final answer, and a provider that
dies mid-conversation aborts the run (503).

Endpoints: `POST api/audit/ai/engagements/{id}/agent` (capability `audit.agent`:
engagement overview, risks, procedures, working papers + detail, findings, evidence,
imported trial balance, and the linked accounting context — availability enforced
server-side) and `POST api/accounting/ai/entities/{id}/agent` (capability
`accounting.agent`: entity overview, chart of accounts, periods, journal entries + detail,
general ledger, trial balance, statements, reconciliations + detail). Both are
activity-logged (`ai.agent`) and appear in the capability catalogs, included only for
plans whose `ai_max_tier` is `agentic` (Audit Firm/Enterprise, Accounting Enterprise).

## 9. Configuration **[implemented]**

`Ai:*` in appsettings (placeholders committed; real keys in git-ignored
`appsettings.local.json`):

```
Ai:Ollama:BaseUrl                     http://localhost:11434
Ai:OpenAI:BaseUrl|ApiKey
Ai:Anthropic:ApiKey
Ai:OpenClaw:BaseUrl|ApiKey
Ai:Routing:{basic|advanced|reasoning|agentic}:{Provider|Model|MaxOutputTokens}
```

Keys never reach the frontend. Provider HTTP clients have 3-minute timeouts; the Anthropic
adapter uses the official SDK (which retries transient failures itself).
