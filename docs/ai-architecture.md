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
every AI request for the pipeline gate). Steps 1-6 live in `AiEntitlementGate`, shared by
`AiCompletionService` and `AgentRunnerService` so a single completion and an agent loop are
gated identically:

1. `ai_enabled` capability check.
2. `ai_max_tier` vs the workload's required tier (`AiTiers.Allows`).
3. `ai_report_scope` vs the workload's required report scope (`AiReportScopes.Allows`) —
   **[Phase 9.5]** how complete a report the plan may generate.
4. `ai_analysis_scope` vs the workload's required analysis scope (`AiAnalysisScopes.Allows`) —
   **[Phase 9.5]** how far across the record set it may reason.
5. `ai_monthly_units`: the operation's cost in **AI credits** is reserved from the allowance
   before the provider is called — atomically, by `consume_ai_units`, which checks and
   decrements under one row lock (ADR-029). No room means a 402 and no provider call.
6. `ai_max_context_tokens`: context documents share the remaining token budget and are
   truncated per document (`[truncated]` marker); the assembled prompt is then checked against
   the limit (~4 chars/token estimate).

The three ladders are independent, so a plan can buy deeper reasoning without buying a wider
view. A refusal names the capability and the level it needs, which is what lets the client say
which plan unlocks it.

### AI credits **[Phase 9.5.1]**

An operation costs what it is worth, not one unit each: a question costs 1, a document summary
2, a report section 6, a complete draft report 20, a full engagement report 35, an agent
investigation 50, agentic report generation 80. Each capability declares its `Cost` in
`AuditAiCapabilities`, beside the entitlement levels that already gate it, and
`Ai:OperationCosts:<capability>` overrides any of them without a deploy. Credits are a
**product** measure: a fallback from OpenAI down to Ollama charges the same, so swapping a model
or a provider never changes a customer's bill.

Accounting capabilities declare no cost and therefore charge 1 apiece — exactly their previous
behaviour. The Accounting usage model is a later phase.

`ai_usage` remains the per-period counter (the row a limit check reads and the only one that
must be locked); `ai_usage_events` is the attribution ledger — organization, user, module,
capability, credits, client, engagement, timestamp. Both are written by `consume_ai_units`, so
they cannot drift apart, and `release_ai_units` deletes the ledger row when it gives credits
back so the ledger always sums to the counter. Only the service role may execute either.

`IAiUsagePeriodResolver` keys usage on the paid billing period end when there is a live
Active/Trialing subscription, and on the calendar month otherwise. When the provider advances
the subscription the key changes and the allowance refills — nothing resets a row, and the
previous period's total stands.

An agent run is charged once at the start, at the capability's cost, rather than per turn: a
multi-step run is one expensive operation, and paying before it starts means it cannot exhaust
the allowance half way through and return nothing.

**Failure policy (ADR-029):** an operation that produces no result costs nothing — no provider
returned, so the credits are released. An operation whose provider returned keeps its charge
even if the application fails afterwards. A release that itself fails is logged and left; the
units stay spent, and the original error is never replaced.

Refusals carry what a user needs: `AiUsageLimitException` (a 402) states what the action needed,
what is left, when the allowance resets and what the next plan up includes. Successful responses
carry what the call consumed, what remains, and whether the organization is within a fifth of
its limit, so a surface can warn before work starts failing.

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

## 6. Audit AI capabilities **[implemented — Phase 3, extended Phase 9.5]**

Catalog: `Ledgance.Audit.AI.Domain.AuditAiCapabilities` — the single place capability-to-plan
gating is declared. Each capability names the reasoning tier, report scope and analysis scope it
consumes; a plan includes it only when it grants all three. `GET /api/audit/ai/capabilities`
reports each with an `included` flag for the caller's plan and `requiredPlan`, the cheapest plan
that includes it, resolved from the catalogue so the UI names the upgrade without holding plan
rules of its own.

| Cheapest plan | Capability | Endpoint (`api/audit/ai/…`) |
| --- | --- | --- |
| Free | Assistant / engagement Q&A | `POST assistant` |
| Free | Document & evidence summarization | `POST engagements/{id}/summarize` |
| Free | Finding summaries | `POST engagements/{id}/summarize-findings` |
| Free | Engagement summary | `POST engagements/{id}/summarize-engagement` |
| Free | Engagement notes from an observation | `POST engagements/{id}/draft-note` |
| Free | Working-paper wording assistance | `POST engagements/{id}/improve-wording` |
| Micro | Audit planning assistance | `POST engagements/{id}/assist-plan` |
| Micro | Materiality assistance | `POST engagements/{id}/assist-materiality` |
| Micro | Risk suggestions | `POST engagements/{id}/suggest-risks` |
| Micro | Procedure suggestions | `POST engagements/{id}/suggest-procedures` |
| Micro | Working-paper drafting | `POST engagements/{id}/draft-working-paper` |
| Micro | Finding drafting | `POST engagements/{id}/draft-finding` |
| Micro | One report section | `POST engagements/{id}/report-section` |
| Micro-Growth | Engagement-wide intelligence | `POST engagements/{id}/analyze-engagement` |
| Micro-Growth | Evidence coverage & gap analysis | `POST engagements/{id}/analyze-evidence` |
| Micro-Growth | Complete draft audit report | `POST engagements/{id}/draft-report` |
| Micro-Growth | Section regeneration | `POST engagements/{id}/generated-reports/{reportId}/sections` |
| Micro-Growth | Report consistency check | `POST engagements/{id}/generated-reports/{reportId}/consistency` |
| Small | Complex risk / cross-document analysis | `POST engagements/{id}/analyze-risks` |
| Small | Anomaly detection (trial balance) | `POST engagements/{id}/detect-anomalies` |
| Small | Review assistance | `POST engagements/{id}/assist-review` |
| Small | Full engagement report (management / reviewer) | `POST engagements/{id}/engagement-report` |
| Medium | Multi-engagement, client and firm intelligence | `POST portfolio/analyze` |
| Medium | Client and firm-level reporting | `POST portfolio/report` |
| Medium-Growth | Multi-step agentic investigation (§8) | `POST engagements/{id}/agent` |
| Medium-Growth | Agentic report generation | `POST engagements/{id}/agentic-report` |

Review endpoints, gated by permission and engagement role rather than by plan:
`GET engagements/{id}/generated-reports`, `GET …/{reportId}`, `POST …/{reportId}/review`.

### AI report generation **[Phase 9.5]**

Everything above the section level produces a **persisted draft**, not a proposal blob:
`GeneratedAuditReport` (Audit.AI.Domain) holds structured sections, each with the engagement
records the model named as its sources, plus the provider and model that produced it. It enters
the record as `Draft` and leaves that state only through `ReviewGeneratedReportCommand`, which
requires the engagement **Manager or Partner** team role — organization Admin/Owner oversight is
read access, not review authority. Accepting records who took professional responsibility for
working from the draft; it does **not** write `audit_reports` and it does not finalize anything.
The engagement partner still finalizes the audit report through `FinalizeAuditReportCommand`,
which is unchanged. Regenerating a section stores a *new* draft rather than editing the one a
reviewer may be looking at.

Report prompts carry a fixed reporting discipline (`AuditAiPrompts.ReportingDiscipline`): never
invent evidence, procedures, findings, amounts, client details, documentation or conclusions;
write `[NOT IN THE ENGAGEMENT RECORD: …]` where the record lacks something rather than filling
the gap; mark partner-reserved judgments `[PARTNER JUDGMENT]`; cite the records each section
rests on. The model is asked for JSON sections so a draft is individually reviewable and
regenerable; prose that ignores the format becomes a single section rather than a failure.

Agentic report generation (`RunAgenticReportWorkflowCommand`) reuses `AuditAgentTools`, the same
read-only, mediator-dispatched tool set as the investigation agent, bound to one engagement id —
no tool takes an engagement parameter, so the agent cannot widen its own scope. It gathers,
drafts, then checks its draft against what it gathered, and the result is still a draft awaiting
review.

Cross-engagement capabilities resolve their own scope (`PortfolioScope`): the engagements the
caller is assigned to, or every engagement in the organization for Admin/Owner oversight. The
repository is organization-scoped underneath, so no query can reach another tenant.

Plan mapping (from `SubscriptionPlanCatalog`), tier / report scope / analysis scope:
Free → basic / none / document · Micro → advanced / sections / document ·
Micro-Growth → advanced / full_draft / engagement · Small → reasoning / engagement / workflow ·
Medium → reasoning / portfolio / portfolio · Medium-Growth → agentic / agentic / portfolio ·
Enterprise → agentic / custom / portfolio.

Enterprise's `custom` report scope is the architectural seat for customer-specific templates,
methodology and agents; those are negotiated per organization through `entitlement_overrides`
and are **not** shipped behaviour. Nothing in the product advertises them as working features.

Known limits: evidence summarization works from recorded metadata/description (binary file
content extraction is future work). Source citations are the record names the model returns,
matched to nothing structurally — they help a reviewer navigate, they are not verified links.

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
