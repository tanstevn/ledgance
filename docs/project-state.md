# Ledgance — Project State

**Where the implementation currently is.** For what the product should be, read
`project-context.md`. This document is updated at the end of every phase.

**Last verified:** 2026-08-13, after Phase 9.5.1. The backend build, the full test suite,
`next build`, the frontend typecheck and ESLint were re-run against the repository on that date,
and the anonymous plan catalogue was rendered end to end against a locally running API; the
live-Supabase findings below come from the earlier bring-up session.

---

## Position

| | |
| --- | --- |
| Last completed phase | **Phase 9.5.1 — Audit AI usage, credits and plan-based consumption** |
| Current phase | none in progress |
| Next phase | **Phase 10 — Security & authorization review** (not started) |

Phase numbering is unchanged: Phase 9 remains Stripe; Phase 9.5 is an additional phase after it.
Accounting's own plan restructure and AI-per-plan strategy are **deliberately not** part of
Phase 9.5 and remain to be done in a separate phase.

---

## Build and test status

Verified by running the commands, not assumed.

| Check | Result |
| --- | --- |
| `dotnet build backend/Ledgance.slnx` | succeeded — 0 errors, 0 C# warnings (only the pre-existing `NU1903` OpenApi advisory) |
| `dotnet test backend/Ledgance.slnx` | **373 passed, 0 failed** (126 shared, 145 audit, 90 accounting, 12 integration) — re-run 2026-08-13 after Phase 9.5.1 |
| API smoke test | boots clean; `api/subscriptions/plans` → 200 anonymous with all eleven plan codes and their AI ladder values; `api/billing/overview` → 401 unauthenticated; `api/billing/webhook` with an invalid signature → 400 and nothing written |
| Frontend | `next build` succeeded · `npx tsc --noEmit` clean · ESLint 0 errors (1 pre-existing warning in the vendored `hooks/use-toast.ts`) |
| Pricing page | rendered against a live API: the whole Audit ladder (Free → Micro → Micro-Growth → Small → Medium → Medium-Growth → Enterprise) with correct capacity and per-plan AI capabilities, and Accounting unchanged |
| **Live end-to-end** | signup → onboarding → organization → audit client verified against a real Supabase project (see the bring-up section) |

---

## What is implemented

### Backend project inventory

35 projects. Carrying real behaviour: `Shared.Application`/`Shared.Infrastructure`,
`Audit/{Client, Engagement, AI}`, `Accounting/{Ledger, AI}`,
`Integration/Ledgance.Integration.AccountingContext`, `Ledgance.Api`, and five test projects.

**Scaffolds** (a `MediatorAnchor` only, with empty `*.Domain` projects): `Accounting/Client`,
`Accounting/Organization`, `Accounting/User`, `Audit/Organization`. `Audit/User` holds a single
query (`GetOrganizationMembersQuery`). `Accounting.AI.Infrastructure` is still empty — AI
context assembly needs no infrastructure and the providers live in `Shared.Infrastructure/Ai`;
`Audit.AI.Infrastructure` gained its first content in Phase 9.5 (generated-report persistence).
All are
registered in `Ledgance.Api/DependencyInjection.cs` so the boundary exists before the code does.

### Platform (Shared)

- **Onboarding** — `POST /api/onboarding/organization` → `ProvisionOrganizationCommand`
  (Shared.Application) creates the organization + Owner membership via `IOrganizationDirectory`.
  `[AllowWithoutOrganization]` lets it run for an authenticated user with no membership.
- **Principal vs organization context** — `CurrentUserMiddleware` records an
  `AuthenticatedPrincipal` for every verified token and a full `CurrentUser` only when
  membership exists; `AuthorizationBehavior` requires full context by default.
  `GET /api/session` returns `needsOnboarding: true` for member-less users.
- **Activity trail** — `IActivityRecorder`/`IActivityReader` (Shared.Application) over an
  append-only `activity_log` table. A summary is recorded as an **active-voice predicate**
  ("approved the audit plan for FY2026 Financial Statement Audit."), never a standalone
  sentence, so every reader renders one sentence by prefixing the actor —
  `activitySentence()` in `frontend/lib/activity.ts` produces "You approved the audit plan
  for …" or "Sarah Whitman approved …". Rows written before 2026-08-11 hold the old passive
  phrasing and will read oddly after the actor name; the trail is append-only, so they are
  left as recorded. Two workflow tests assert the predicate form. The scope column is the product-neutral **`context_id`**
  (renamed from `engagement_id` in Phase 4 — ADR-020): the engagement in Audit, the accounting
  entity in Accounting. Every mutating Audit and Accounting handler records to it.
- **`DomainRuleException`** → HTTP 409 in `ExceptionHandlerMiddleware`.

### Audit

Client feature (`Modules/Audit/Client`), Engagement module (`Modules/Audit/Engagement`, 39
command/query slices including the Phase 9.5 plan-usage query, team confinement per ADR-017),
Audit AI module (25 capabilities behind the shared AI orchestrator — 10 completion capabilities
from Phase 3, `audit.agent` from Phase 7, and 14 added in Phase 9.5, ADR-018/ADR-022/ADR-027).
Everything except the persisted generated reports is proposal-only; generated reports are
persisted drafts that a manager or partner must review. Phase 6 added the linked-accounting
slices inside Engagement. See `implementation-status.md` Phases 2–3, 6 and 7 for the full inventory.

### Accounting — Ledger module (`Modules/Accounting/Ledger`) — new in Phase 4

One module hosts every books-scoped feature (ADR-019), mirroring the Engagement-module
rationale: one aggregate cluster (entity + fiscal period), one guard, one lifecycle.

**Domain** (`Ledgance.Accounting.Ledger.Domain`), the rule-bearing core:

- `AccountingEntity` — a set of books; base currency fixed at creation (3-letter ISO);
  archiving requires all fiscal periods closed; entities are never deleted.
- `Account` — typed (`Asset/Liability/Equity/Revenue/Expense`) with derived normal balance;
  hierarchical (sub-account must share the parent's type and entity; parents are summary
  accounts and reject postings); type change blocked once the account has postings or
  children; accounts deactivate rather than delete.
- `FiscalPeriod` — Open/Closed; close blocked while draft entries are dated inside it;
  reopen allowed (Manager+); overlap prevented at creation.
- `JournalEntry` — double-entry: ≥2 lines, each line one-sided with ≤2 decimal places,
  total debits = total credits > 0, memo required. Lifecycle Draft → Posted → Reversed:
  drafts are editable/deletable; posting requires an open period containing the entry date
  and materializes append-only **ledger lines**; posted entries are immutable — the only
  correction is `Reverse`, which builds a swapped-line reversal entry linked both ways.
- `Reconciliation` — per account against an external statement: cleared ledger lines, and
  completion requires a zero difference or a documented explanation; one in-progress
  reconciliation per account.
- `AccountingDocument` — source-document metadata, optionally linked to a journal entry or
  reconciliation.

**Application** — 34 command/query slices in feature folders (Entities, ChartOfAccounts, Periods,
Journal, Ledger, Reports, Reconciliations, Documents, Activity), each colocating
command/validator/handler. `IEntityGuard` centralizes entity existence + archived checks.
General ledger (opening/running/closing balance), trial balance (as of period end, balanced
by construction), income statement (period P&L) and balance sheet (with a current-earnings
line, since no closing entries exist yet) are **derived queries over ledger lines** — no
stored report state. Entitlement limits: `max_entities` on entity creation,
`max_transactions_per_period` on entry creation/reversal (counted per fiscal period),
`storage_bytes` on document upload. Permissions: `accounting:ledger:read` (Viewer+),
`contribute` (Member+; drafts, posting, reconciliation, documents), `manage` (Manager+;
entities, chart of accounts, periods, reversals).

**Infrastructure** — persistence models for 7 tables (journal lines as jsonb on the entry;
ledger lines as their own filterable table), repositories composing `SupabaseRepository<T>`,
`SupabaseDocumentFileStore` (private `accounting-documents` bucket, signed URLs).

### Accounting AI (`Modules/Accounting/AI`) — new in Phase 5

`AccountingAiCapabilities` catalog (10 completion capabilities: 3 basic, 4 advanced,
3 reasoning — plus `accounting.agent` added in Phase 7, so 11 in the catalog today) in
AI.Domain; 10 slices in AI.Application (Assistant, Suggestions, Analysis feature folders)
riding the Phase 3 orchestrator unchanged — authorization → `ai_enabled` → tier → monthly
units → context truncation → tier-routed execution, with Accounting usage metered under
`ProductModule.Accounting`. Context is assembled by `LedgerAiContext` from the Ledger
module's own repositories (entity overview, chart of accounts, entries, computed trial
balance/statements, reconciliation state); every capability is entity-guarded via
`IEntityGuard`, activity-logged (`ai.*`, entity as context id) and returns a proposal-only
`AiProposalResult`. The close-review capability requires `accounting:ledger:manage`. See
`ai-architecture.md` §7 for the capability/endpoint table. `Accounting.AI.Infrastructure`
remains empty (context assembly needed no infrastructure).

### Agentic AI — OpenClaw (Phase 7)

`IAgentRunner`/`AgentTool` contracts (Shared.Application/Ai) + `AgentRunnerService`
(Shared.Infrastructure/Ai): agentic-tier gate, one usage unit per provider turn re-checked
against the monthly limit, capped tool steps with a forced no-tools final turn, and
containment of tool denials as transcript results (ADR-022). `OpenClawAgentClient` drives
the loop natively over `v1/agent/turns` (OpenClaw chooses tools; execution never leaves the
app); `ChatAgentAdapter` drives the same loop over the chat providers via strict JSON as
the downward fallback. `audit.agent` (engagement records + linked accounting context) and
`accounting.agent` (the entity's books) expose read-only tool whitelists of mediator
requests re-entering the full pipeline as the calling user, with the scope id fixed
server-side. Endpoints: `POST api/audit/ai/engagements/{id}/agent`,
`POST api/accounting/ai/entities/{id}/agent` — proposal-only `AgentRunReport` with the step
transcript; included only for agentic plans (Audit Firm/Enterprise, Accounting Enterprise).
See `ai-architecture.md` §8.

### Billing and subscriptions (`Shared/*/Billing`) — new in Phase 9

Stripe sits behind ports in `Shared.Application/Billing` (`IBillingGateway`,
`IBillingPriceCatalog`, `IBillingWebhookVerifier`, `ISubscriptionStore`,
`IProcessedEventStore`); `Stripe.net` types appear only in `Shared.Infrastructure/Billing`
(ADR-023). Slices: `StartCheckoutCommand`, `CreateBillingPortalSessionCommand`,
`ChangeSubscriptionPlanCommand`, `SetSubscriptionCancellationCommand`,
`GetBillingOverviewQuery` (all `organization:billing:*`, Owner manages / Admin reads) and the
anonymous `HandleBillingWebhookCommand`.

- **Purchasable is a server fact.** Paid plans map to prices through `Stripe:Prices:<PlanCode>`;
  Free and Enterprise never reach checkout, and a plan without a configured price is refused.
  `GET api/subscriptions/plans` carries `purchasable` so no surface offers a purchase the
  server would decline. A configured value that is not a `price_…` identifier (an amount typed
  by mistake, a `prod_…` id) is rejected at startup with a warning and leaves the plan
  unpurchasable rather than failing at checkout.
- **Displayed prices are the provider's prices.** `IBillingPriceReader` (`StripePriceReader`,
  cached 5 minutes) reads `unit_amount`, currency and interval for each configured price, and
  the plans endpoint returns them; `priceLabel()` in `lib/plans.ts` formats them for every
  pricing surface. A plan the provider has no price for falls back to the product-documented
  label (Free `$0`, Solo `$14.99/month`, Enterprise `Contact sales`) or reads "Pricing at
  launch" — the frontend never invents an amount. A provider outage degrades to those
  fallbacks; the anonymous pricing page still renders.
- **Scope travels as signed metadata.** Checkout stamps organisation, module and plan onto the
  subscription, so later events match without trusting request input. Afterwards the billed
  **price** decides the plan, which keeps changes made in Stripe's own portal in sync.
- **Webhooks are the truth** (ADR-007): signature verified before the payload is read, event ids
  recorded in `billing_events` for idempotency, and events older than the row's `last_event_at`
  discarded. Handled: `checkout.session.completed`,
  `customer.subscription.created|updated|deleted`, `invoice.paid`, `invoice.payment_failed`.
- **Entitlements were not touched.** Billing writes `organization_subscriptions`; the existing
  entitlement service resolves from it, so cancellation returns an organisation to Free by
  status alone. Cancelling sets `cancel_at_period_end` — access continues until the provider
  ends the subscription.
- `SupabaseSubscriptionStore` bypasses `SupabaseRepository` because the webhook path has no user
  and therefore no organisation context; it filters on ids the application resolved itself.
- **Checkout persists the customer before opening the session**, so a failure part-way through
  never leaves a provider customer with nothing pointing at it; the retry reuses it. Persistence
  models initialise every collection property — an insert serialises all properties, so a null
  collection is written as SQL null instead of falling back to a NOT NULL column's default
  (this is what broke the first live checkout, on `entitlement_overrides`). A reflection test
  guards the whole class of bug for the shared models.

### Audit plans, AI entitlements and report generation — new in Phase 9.5

**Audit plan structure replaced.** `AuditProfessional`/`AuditOrganization`/`AuditFirm` are gone;
the ladder is now Free → Micro → Micro-Growth → Small → Medium → Medium-Growth → Enterprise,
declared once in `SubscriptionPlanCatalog`. Capacity per plan (users / clients / engagements /
storage): Free 3 / 1 / 2 / 5 GB · Micro 15 / 30 / 75 / 250 GB · Micro-Growth 40 / 100 / 300 /
500 GB · Small 90 / 250 / 800 / 750 GB · Medium 150 / 500 / 1,300 / 2 TB · Medium-Growth 200 /
unlimited / unlimited / 6 TB · Enterprise unlimited on every dimension. A stored row naming a
retired code resolves to Free in code (`SupabaseSubscriptionReader` parses with a Free
fallback); migration `0011` maps existing rows onto the closest new plan so a paying
organisation does not silently drop to Free. The catalogue also gained `Ordered(module)` and
`NextAbove(plan)` so "the next plan up" is a server fact.

Because `Free` is one shared code, raising Audit Free to 3 users and 5 GB raised Accounting Free
too. That is an increase, not a restriction, and Accounting's own plan values are otherwise
untouched — the Accounting restructure is a later phase.

**Two new ordered entitlements** sit beside `ai_max_tier`: `ai_report_scope`
(`none` → `sections` → `full_draft` → `engagement` → `portfolio` → `agentic` → `custom`) and
`ai_analysis_scope` (`document` → `engagement` → `workflow` → `portfolio`). They are independent
of the tier, which is what lets Micro buy `advanced` reasoning without buying whole-report
writing. `AiEntitlementGate` (Shared.Infrastructure/Ai) is now the single plan check for both
`AiCompletionService` and `AgentRunnerService`, so one completion and an agent loop are gated
identically. A granted value outside its ladder ranks below every level, so a typo or a tampered
override denies rather than escalates.

**Audit AI capabilities grew from 11 to 25**, each declaring the tier, report scope and analysis
scope it consumes. New slices: finding/engagement summaries, engagement notes and working-paper
wording (Free); audit-plan and materiality assistance, and single report sections (Micro);
engagement-wide intelligence, evidence-gap analysis, complete draft reports, section
regeneration and report consistency checking (Micro-Growth); full engagement reporting for
management and reviewers (Small); portfolio intelligence and client/firm reporting (Medium);
agentic report generation (Medium-Growth). `GET api/audit/ai/capabilities` now also returns
`requiredPlan` — the cheapest plan including that capability, resolved from the catalogue — so
the UI names the upgrade without holding plan rules.

**Generated reports are persisted drafts under review.** `GeneratedAuditReport`
(Audit.AI.Domain) holds structured sections with the engagement records the model cited, plus
the provider and model that produced it; it is stored in `audit_generated_reports`
(migration `0011`, RLS read-scoped like every other Audit table) through the previously empty
`Ledgance.Audit.AI.Infrastructure`. A draft leaves `Draft` only via `ReviewGeneratedReportCommand`,
which requires the engagement **Manager or Partner** team role — organization Admin/Owner
oversight is read access, not review authority. Accepting records who took responsibility; it
never writes `audit_reports`, and `FinalizeAuditReportCommand` (partner, no open findings) is
unchanged. Regenerating a section stores a *new* draft rather than editing one a reviewer may be
reading.

Report prompts carry a fixed anti-fabrication discipline: no invented evidence, procedures,
findings, amounts, client details or conclusions; `[NOT IN THE ENGAGEMENT RECORD: …]` where the
record is short; `[PARTNER JUDGMENT]` on anything reserved to the partner; sources cited per
section. Agentic generation reuses `AuditAgentTools` — the same read-only, mediator-dispatched
tools as the investigation agent, bound to one engagement id, no tool taking an engagement
parameter. Cross-engagement capabilities resolve scope through `PortfolioScope`: the caller's
assigned engagements, or the whole organization for Admin/Owner oversight.

**Billing experience.** `GET api/audit/subscription/usage` reports users, clients, engagements,
storage and AI actions used against their limits, counted server-side. The billing page shows
current usage against capacity, what AI can do on the plan, and a "next step up" panel listing
only what actually changes; the plan picker compares capacity and AI side by side. Every bullet
is derived from the entitlement values the server sent, so nothing is advertised that the server
would refuse. `Stripe:Prices` in `appsettings.json` now carries placeholders for the five
purchasable Audit codes.

### AI usage, credits and consumption — new in Phase 9.5.1

**An AI operation costs what it is worth.** Every capability declares a `Cost` in AI credits
alongside its entitlement levels, in the same catalogue (`AuditAiCapabilities`), so pricing an
operation is one line next to the gating that already governs it. Current Audit prices: an
assistant question or a summary 1, a document summary 2, planning/risk/procedure/finding
assistance 3–4, a report section 6, evidence or engagement-wide analysis 8, a complete draft
report 20, a consistency check 10, reasoning-tier analysis 12, a full engagement report 35,
portfolio intelligence 25, portfolio reporting 40, an agent investigation 50, agentic report
generation 80. `Ai:OperationCosts:<capability>` overrides any of them without a deploy.

Credits are a **product** measure, not a provider one — a fallback from OpenAI down to Ollama
charges the same, which a test asserts, so swapping a model never changes a customer's bill.

**Allowances were rescaled** for the new prices (Audit only; Accounting's are untouched):
Free 200 · Micro 12,000 · Micro-Growth 40,000 · Small 120,000 · Medium 300,000 ·
Medium-Growth 750,000 · Enterprise unlimited. Free stayed at 200 deliberately — raising it
would have raised Accounting Free too, since `Free` is one shared plan code, and 200 credits
still buys 200 assistant questions on a plan whose most expensive capability costs 2. Enterprise
capacity is negotiated through `entitlement_overrides` rather than a fixed ceiling in code.

**Usage is reserved before the work runs, not recorded after.** `consume_ai_units` (migration
`0012`) takes the credits and writes the ledger row under one `for update` lock on the counter
row; returning no row means the allowance would have been exceeded and the operation is refused.
This is what makes concurrent requests safe — a read-then-write from the application would let
two simultaneous callers both see the same remaining balance and both proceed. A test drives ten
concurrent operations at a 10-credit remainder and asserts exactly two succeed.

**Failed operations follow one rule: an operation that produces no result costs nothing.** If no
provider produced anything — an outage, every tier failing, an agent whose provider dies
mid-conversation — `release_ai_units` gives the credits back and deletes the ledger row, so the
ledger always sums to the counter. Once a provider has returned, the credits stay spent even if
the application fails afterwards. A release that itself fails is logged and left; the units stay
consumed, which is the safe direction, and it never replaces the original error.

**An agent run is charged once, up front, at the capability's cost** rather than per provider
turn. A multi-step run is one expensive operation, and paying for it before it starts means a
run cannot exhaust the allowance half way through and return nothing. The loop stays bounded by
the workload's tool-step budget.

**Two tables.** `ai_usage` remains the per-period counter (the row a limit check reads and the
only one that must be locked); `ai_usage_events` is the attribution ledger — organization, user,
module, capability, credits, client, engagement, timestamp. Both are written by the same
function so they cannot drift apart. Organization-scoped RLS read, service-role-only execute on
both functions.

**The period follows the subscription.** `IAiUsagePeriodResolver` keys usage on the paid billing
period end (`sub:2026-09-14`) when there is a live Active/Trialing subscription, and on the
calendar month otherwise — which is what a Free organization gets. When Stripe advances the
subscription the key changes and the allowance refills; nothing has to reset a row. Upgrades,
downgrades and cancellations need no special handling: the entitlement service already resolves
the current plan, and a non-Active subscription resolves to Free.

**Refusals are actionable.** `AiUsageLimitException` (a 402, like every entitlement refusal)
says what the action needed, what is left, when the allowance resets and what the next plan up
carries — with no provider, model or internal detail in it. `GET api/audit/subscription/usage`
now also returns the AI credit balance and reset date; AI responses carry what the call
consumed, what remains and whether the organization is within a fifth of its limit.

**Frontend.** A credits strip above the engagement AI tools shows the balance and reset date and
links to billing once four fifths are spent; proposals and agent reports show what they cost;
the billing page meters AI credits alongside capacity and gives each plan a one-line statement
of what its AI capacity is for. No AI-only dashboard was added.

### Accounting ↔ Audit integration (`backend/Integration`) — new in Phase 6

`Ledgance.Integration.AccountingContext` (ADR-021) is the only assembly referencing both
contexts, referenced only by the host. Accounting publishes `IAccountingReadContract`
(Ledger.Application/Published — entity/period/trial-balance snapshots from posted ledger
lines only); Audit owns `ILinkedAccountingSource` in its own vocabulary next to the CSV
baseline; the integration adapter re-verifies on every call that both products carry the
`accounting_context_sharing` entitlement (402 otherwise) and that an Admin/Owner enabled
the per-organization link (`integration_accounting_links`, migration 0005; 409 when off).
Audit can browse linked entities/periods and import a trial balance stamped
`TrialBalanceSource.LedganceAccounting`, with the source entity, period and as-of date
recorded in the audit trail. Link management: `integration:accounting_link:read` (Viewer+)
/ `manage` (Admin+), enable requiring both entitlements. Audit remains fully functional
without Ledgance Accounting — the external-file source is untouched.

### API surface

Phase 6 added `api/integration/accounting-link` (GET status, PUT set),
`GET api/audit/accounting-context` and
`POST api/audit/engagements/{id}/trial-balance/from-accounting`. Other `api/audit/*` routes
unchanged. `api/accounting/entities` route family: entities (CRUD +
archive + activity), `{entityId}/accounts`, `{entityId}/periods` (+close/reopen),
`{entityId}/journal-entries` (paged list, detail, draft update/delete, post, reverse),
`{entityId}/general-ledger`, `{entityId}/trial-balance`, `{entityId}/reports/*`,
`{entityId}/reconciliations` (cleared-lines, complete, cancel), `{entityId}/documents`
(multipart upload, 25 MB command limit, signed download URLs). Phase 5 added
`api/accounting/ai/*` (assistant, per-entity/period AI actions, capability listing with
per-plan `included` flags). Post-Phase 8 added `GET api/audit/activity` (team-confined
recent feed), `GET api/accounting/activity`, and `POST api/organization/products`
(Owner-only product activation).

The list surfaces page server-side: `GET api/audit/clients/paged` (existing, now carrying
contact/website and per-client engagement counts), `GET api/audit/engagements/paged`
(page + `status` + `clientId` + name search) and `GET api/accounting/entities/paged`
(page + name search, with fiscal-period counts).

The full controller set is: `Accounting/` (entity, account, period, journal, ledger,
reconciliation, document, activity, AI), `Audit/` (client, engagement, fieldwork,
working paper, evidence, finding, accounting-context, activity, user, AI),
`Integration/` (accounting link), plus Onboarding, Organization, Session, Subscriptions and
Billing. `GET api/audit/users` lists organization members for team pickers — the only slice the
otherwise-scaffolded `Audit/User` module carries.

Phase 9 added `api/billing/*`: `GET overview`, `POST checkout`, `POST portal`,
`POST change-plan`, `POST cancel`, and the anonymous signature-verified `POST webhook`.

### Post-Phase 8 — live Supabase bring-up (2026-08-10/11)

The stack ran against a **real Supabase project** for the first time. Walking the flow
surfaced and fixed four latent defects, each at a deeper layer:

1. **JWT validation for asymmetric projects** — the JWKS fallback pointed ASP.NET's
   `MetadataAddress` at the raw JWKS document (which expects an OIDC discovery document
   Supabase doesn't publish). Keys are now resolved from the JWKS URL directly, cached for
   the process lifetime; token-validation failures are logged (reason only, never token
   contents). An empty `Supabase:JwtSecret` selects this path; a value selects HS256.
2. **API role grants** — newer Supabase projects no longer auto-grant Data API roles on
   `public`; migration `0006_api_role_grants.sql` grants service_role full access,
   authenticated read-only (the RLS backstop), nothing to anon, and sets default
   privileges for future tables.
3. **Primary keys were never sent on insert** — every persistence model declared
   `[PrimaryKey("id", false)]`, so Postgres generated its own ids while the code kept
   referencing the C#-generated ones (first symptom: FK violation creating an
   organization's owner membership). All 24 models now insert their client-generated ids.
4. **Enum JSON binding** — request bodies carry enum names ("FinancialStatement");
   `JsonStringEnumConverter` is now registered globally, matching the response DTOs'
   `ToString()` convention.

Verified live: signup (email confirmation off for local testing) → onboarding →
organization + Owner membership → audit client creation, with RLS and role grants
confirmed by SQL probes. Journal posting, storage uploads and reconciliation remain to be
walked live (see Known issues).

### Frontend (Phase 8 — discovery/subscription scope)

Marketing: two-platform landing (`/`) with platform chooser and ecosystem/security
sections grounded in implemented capabilities (all fabricated content removed);
`/accounting` and `/audit` product pages; `/pricing` with per-platform tabs. All pricing
surfaces derive plan features from `GET /api/subscriptions/plans` — a new anonymous
endpoint exposing `SubscriptionPlanCatalog` verbatim, so marketing cannot drift from what
the backend enforces. Prices follow the product docs exactly: Free $0, Accounting Solo
$14.99/mo, Enterprise = Contact Sales, everything else "Pricing at launch".

Flows: signup carries platform/plan intent → `/onboarding` provisions the organization via
the real API → `/subscribe` (plan summary, then real Stripe Checkout for a purchasable plan,
Contact sales for Enterprise, with "continue on Free" always available) →
`/subscribe/success`, which shows a subscription as active only when `/api/session`
confirms it. **Billing management** (Phase 9) lives on `/dashboard/billing`: per-platform plan
and status, renewal or end date, a past-due warning, a plan picker that buys or switches, the
Stripe portal for payment methods and invoices, and cancel/resume — all Owner-only, with the
buttons hidden for anyone without `organization:billing:manage`. Any 402 from any endpoint
raises a single upgrade offer through `components/upgrade-prompt.tsx`. The cross-platform recommendation (`components/cross-sell.tsx`) renders only
for server-confirmed qualifying paid plans (Solo→Professional band on either side — never
Free, never Enterprise, never during initial discovery) and is always skippable
(explore / maybe later / permanent dismiss).

Dashboard: session-driven layout (org name + per-module plan badges from the extended
`SessionResponse`, onboarding gate), overview with live counts, Accounting entities and
Audit clients pages (real list + create with loading/empty/error/populated states), and a
plans & billing page. `lib/mock-data.ts` is gone — no product surface is mock-driven.

**Product workspaces:** the dashboard now carries working feature surfaces for both
products, wired to the real APIs with loading/empty/error/populated states throughout.
Audit: clients, engagements (list + create) and a per-engagement workspace
(`/dashboard/audit/engagements/{id}`) with tabs for overview (plan + materiality editing,
plan approval, status transitions), team assignment, risks, procedures (start/complete
with conclusions), working papers (create + ordered prepare/review/approve sign-offs),
versioned evidence upload, trial balance (CSV import and linked-books import when sharing
is active), findings (raise/resolve), the audit report (draft/finalize) and the activity
trail. Accounting: entities plus a per-entity workspace (`/dashboard/accounting/{id}`)
with tabs for the chart of accounts, fiscal periods (open/close/reopen), the journal
(balanced multi-line entry editor, post/reverse/delete, pagination), live trial balance +
income statement + balance sheet by period, reconciliations, source-document upload and
activity. Deeper interactions (review-notes UI, reconciliation line-clearing) remain
future work.

**List surfaces (2026-08-11 UI round):** the two record lists and the engagement file were
rebuilt against the paged endpoints above.

- **Clients** (`/dashboard/audit`) and **Entities** (`/dashboard/accounting`) are card grids —
  up to five cards per row (`2xl:grid-cols-5`, degrading to 4/3/2/1) — fetched **10 at a time
  and extended by infinite scroll**: an `IntersectionObserver` sentinel (`InfiniteScrollSentinel`)
  asks `useApiInfiniteQuery` for the next page as the user reaches the end. Each client card
  shows the identity tile, active-engagement badge, industry, email/phone/website and a total
  engagements link into the filtered engagement list; each entity card shows open/total fiscal
  periods, base currency and a link into its books. Both have a debounced server-side name search.
- **Engagements** (`/dashboard/audit/engagements`) uses **numbered pagination** — 10 per page,
  page 1 default, first/last plus a window with ellipsis (`Pagination`) — over
  `GET api/audit/engagements/paged`. The filters are **status** and **client** (plus a name
  search), all applied server-side; `?clientId=` preselects the client filter so a client card
  can deep-link into it. Rows show the client tile, name + status, period, budget hours, type and
  a five-step **stage** indicator derived from the engagement status.
- **The engagement file** (`/dashboard/audit/engagements/{id}`) opens on a header card (client
  tile, name, partner/manager from the assigned team, period, fiscal year end, an **Edit**
  dialog wired to `UpdateEngagementCommand`, and the status pill with a **circled chevron
  menu** — a dropdown of the other stages with one-line meanings; the server's stage gates
  still decide) and four stat tiles (stage-gate progress %, budget hours + team size, working
  papers + approved, open review notes). **Four primary tabs** styled as a segmented bar —
  Overview, Documents (the evidence surface, with count), Working Papers (with count), Team —
  with everything else (Planning, Risks, Procedures, Trial balance, Findings, Report, AI,
  Activity) behind a **More** menu at the end of the bar. The overview holds the working-paper
  sign-off breakdown and the needs-attention list (entries jump to the owning tab); plan,
  materiality and engagement-fact editing moved intact to the Planning tab. Every figure comes
  from `EngagementDetail.Progress` or the working-paper list — the engagement domain records
  no actual hours, so no "budget used" is shown; there is no Export (nothing to export to
  yet).
- **Form controls (2026-08-11).** `components/workspace.tsx` gained two shared fields, used by
  the create-engagement dialog and available to every other form:

  `SelectField` — a Radix select whose list items may carry a second hint line (the client
  picker shows each client's industry). Three fixes the vendored `SelectTrigger` forced:
  explicit children are passed to `SelectValue` so the trigger renders **only the label** —
  by default Radix renders the whole selected item, and `line-clamp-1` then collapsed the
  hint line into a trailing ellipsis; `gap-2` plus truncation keeps a long value off the
  chevron, which `justify-between` alone does not; and an optional leading `icon` sits in a
  flex row forced with `!flex`, because the vendored `[&>span]:line-clamp-1` applies
  `display:-webkit-box` to any direct span child and would otherwise break that row. The
  listbox renders at `z-[70]`, above the date popover, which is itself above the dialog.

  `DateField` — a calendar in a popover replacing the native date input. It stores the ISO
  `yyyy-MM-dd` string the API's `DateOnly` expects and parses at local noon, so a picked date
  never shifts a day across timezones. The popover is portalled with collision padding at
  `z-[60]`, so the dialog never clips it. **The month/year header is ours**, not
  react-day-picker's: its dropdown caption layers a transparent native `<select>` over a
  visible label, so styling the select renders both (this shipped once and had to be
  undone). The library's caption and nav are hidden, the displayed month is driven by
  `month`/`onMonthChange`, and the header is two `SelectField`s between spaced arrow
  buttons. `fixedWeeks` keeps the grid at six rows so the popover does not resize as the user
  pages between months, the grid is centred against the wider header rather than
  left-aligned, and the header widths are sized so the longest month name and the year both
  render without truncation (`min-w-0` on the month select stops a long name pushing the next
  arrow out of the popover). `components/ui` is left unmodified throughout (ADR-013).

  `FieldSelect` (native) remains for dense inline filters where a full listbox is heavier
  than the job needs.
- **Client cards are fully clickable** — the whole card links to that client's engagements, with
  a "View details →" affordance revealed on hover *and* keyboard focus.
- **Documents round (2026-08-11).** Evidence gained real **versioning with retained history**
  (migration `0010`): superseding now pushes the outgoing version — path, size, note,
  uploader, date — into `version_history` instead of overwriting in place, re-uploading an
  existing file name **auto-versions** that document (previously the UI claimed this but the
  frontend never sent `supersedesEvidenceId`, so duplicates were created), and
  `GET …/download-url?version=N` serves any retained version. Evidence also gained a
  **category** (Evidence/Financial/Correspondence/Supporting) and normalized **tags**.
  Uploads on both platforms accept **multiple files** — one command per file through the full
  pipeline, so per-file limits still apply; a mid-batch failure names the file and keeps what
  landed. The Documents tab is a **card grid** (up to 4 per row) with client-side search and
  category chips; a card opens a **details modal** showing badges, tags, uploader/updated/
  version tiles and, below a separator, the version history with an **Upload new version**
  button beside the heading — opening it widens the dialog into **two balanced columns**
  (details left, upload panel right, a link icon on the divider; stacked on small screens)
  rather than stacking a second modal. The panel takes a single file with an optional
  description and sends an explicit `supersedesEvidenceId`. The current version offers
  Download, older versions **View**; the modal selects by id so it re-renders live from the
  refetched list after an upload. Both platforms'
  upload flows are a button-opened modal around the shared `FileDropZone` (drag-and-drop +
  browse, chip list, per-file remove). Accounting documents keep their flat list and have no
  category/tags/versioning — out of scope by request.

**Overview redesign:** the dashboard overview is platform-aware and data-dense. Audit:
four stat cards (active engagements/clients, in fieldwork, in review, period ends ≤30
days), an active-engagements list with color-coded deadline chips, an **upcoming
deadlines** panel (sorted by period end; overdue red, ≤30d amber) and a **recent
activity** feed. Accounting: entity cards into their books plus its own activity feed.
Feeds are backed by new org-wide endpoints — `GET /api/audit/activity` (confined to the
caller's team engagements per ADR-017, covered by a test) and
`GET /api/accounting/activity` (role-based, unconfined) — via
`IActivityReader.ListRecentAsync` and `ITeamRepository.ListEngagementIdsForUserAsync`.
The signed-in user's own entries render as **"You"**; others by name, with relative
timestamps.

**AI surface (both platforms, plan-gated):** "AI assistant" nav pages
(`/dashboard/audit/ai`, `/dashboard/accounting/ai`) with an assistant ask-box (optional
engagement/entity context), an agent section, and a capability grid where locked
capabilities show their tier and an upgrade link — the `included` flags come from the
capabilities endpoints, never client logic. Every engagement workspace has an **AI tab**
exposing all ten Audit capabilities as tool cards (summarize paper, suggest
risks/procedures, draft paper/finding, risk analysis, anomaly detection, review
assistance, report draft, agent with tool-trail display); every entity workspace has the
Accounting equivalent (period summary/statement explanation/anomalies/financial
analysis/close review via a period selector, entry suggestion, two-period variance,
agent). Journal rows carry a basic-tier **Explain** action. All output renders as
proposal cards with provider/model/tier provenance and the permanent disclaimer. The Free
plan genuinely includes the basic-tier capabilities (200 actions/month, server-enforced);
completions require a live provider — Ollama at `localhost:11434` serves the basic tier.

**Auth redesign:** login and signup rebuilt in modern SaaS form — icon-only **Google and
LinkedIn (OIDC)** OAuth buttons (accessible labels/tooltips, per-provider loading,
graceful "provider not enabled" errors) above an "or continue with email" divider;
email/password retained; an inline forgot-password flow (request → sent confirmation, no
account-existence leak) on login; signup keeps the platform-aware panel and carries
platform/plan intent through the OAuth redirect into onboarding. Providers require
enabling in the Supabase dashboard (`signInWithOAuth` via the shared
`components/auth/social-buttons.tsx`; `auth-context` gained `signInWithOAuth`).

**Visual system:** the tinted ambient background (`bg-ambient`: cool base + fixed
emerald/sky washes, dark-navy variant) now applies app-wide from the root `<body>`; all
decorative absolute overlays are `pointer-events-none` (they previously swallowed clicks
— the cause of dead hero buttons and pricing tabs); `color-scheme` follows the active
theme so native controls (date pickers, select popups) match; native selects use the
shared `FieldSelect` with a custom chevron.

**Platform scoping:** the platform chosen at signup is persisted on the organization
(`organizations.products`, migration 0007) and returned by `/api/session` as `Products`
(union of the stored choice and any paid modules). The dashboard — navigation, overview,
plan badges, product pages — renders only activated platforms; direct URLs to a
non-activated platform show an activation notice. The owner can activate the other
platform free via `POST /api/organization/products`
(`EnableOrganizationProductCommand`, `organization:manage`), surfaced as an
"Activate free" card on plans & billing. Entitlements are unaffected — this scopes what
the UI offers, and the backend remains the enforcement layer.

### Database

`supabase/migrations/0004_accounting_core.sql` — renames `activity_log.engagement_id` →
`context_id`, then 7 accounting tables with FKs/checks/indexes (including one-sided-amount
checks on ledger lines and unique `(entity_id, code)` / `(entity_id, entry_number)`), the
`accounting-documents` storage bucket, and org-scoped read RLS on everything.
**Migrations 0001–0007 were confirmed applied to a live Supabase project** (0006 grants the
Data API roles newer projects no longer receive by default; 0007 adds `organizations.products`
for platform scoping); RLS and `*_read_own` policies verified by SQL probes. **0008 (billing),
0009 (token claims hook), 0010 (evidence versioning), 0011 (generated audit reports plus the
Audit plan-code remap) and 0012 (AI usage accounting) were written after that session and have
not been confirmed applied** — the features that need them will fail against a project still on
0007. Without 0011, AI report generation cannot store a draft, and any organisation still
holding a retired Audit plan code resolves to Free. Without 0012, **every AI request fails**:
the reservation calls `consume_ai_units`, which will not exist. 0012 has been reviewed for
plpgsql correctness but not executed — no Postgres was available in this session.

### Tests — 373 passing

- Shared (126): +15 in Phase 9.5.1 (`AiUsageAccountingTests`) — an operation consuming what it
  declares, expensive operations costing more than cheap ones, configuration repricing an
  operation, an operation costing more than the remainder refused outright, the refusal naming
  the reset date and the next plan's allowance while exposing no provider or internal detail,
  unlimited never refused but still recorded, a negotiated Enterprise allowance replacing
  unlimited, work that never reached a provider giving its credits back, a capability outside
  the plan costing nothing, **ten concurrent operations against a 10-credit remainder yielding
  exactly two grants**, usage attributed to the calling organization/user/engagement, a new
  period starting from an empty allowance while the previous period's total stands, the
  approaching-limit flag raised before the limit is hit, isolation between organizations, and
  Audit and Accounting metered separately. Plus +19 in Phase 9.5 — the full Audit capacity and storage ladders, a monotonicity
  guard asserting no dimension or AI ladder ever goes backwards as a plan gets more expensive
  (per product, so the shared Free row's cross-product allowance does not trip it),
  `NextAbove` staying inside its own product, a retired plan code no longer parsing, every plan
  publishing the three AI ladder values, the report and analysis scopes each refusing a workload
  the tier alone would allow, the same workload passing once the plan grants the scope, and an
  unrecognised ladder grant denying rather than escalating. Plus 16 billing tests (Phase 9: permission gate on checkout, Free and Enterprise
  refused, unpriced plan refused, session URL + metadata + customer reuse, checkout blocked
  when a subscription is active, unverified webhook rejected, entitlements following a
  subscription event, duplicate delivery ignored, stale event discarded, deletion falling back
  to Free, portal-driven plan change adopted from the price, cancel-keeps-access-until-period-end,
  plan change, change without a subscription, overview contents and its read permission,
  portal requiring a customer), 3 plan-catalog query tests including purchasability, 8
  agent-runner tests (Phase 7) and 3 organization-product tests on top of the Phase 1–6 core.
  Post-Phase 9 added 11 more: the price catalog rejecting non-`price_…` values (amounts,
  `prod_…` ids, placeholders, unknown plan codes), live price/currency/interval on the plans
  row, an unreadable price still rendering the page, the persistence-model null-collection
  guard, and a checkout failure not orphaning its provider customer.
- Audit (145): +13 in Phase 9.5.1 (`AuditAiUsageTests`) — a summary costing what the catalogue
  says, a whole report costing at least ten times a summary, usage attributed to the engagement
  it was spent on, an unauthorized operation consuming nothing, a capability outside the plan
  consuming nothing, a direct API call unable to get past an exhausted allowance, each plan's
  allowance carrying a stated number of runs of the operation it is sold on (six plans), and a
  provider fallback not changing the price. Plus +51 in Phase 9.5. Plan matrix (10): what each of the seven plans includes and
  excludes, asserted through the capability catalogue the API serves, plus every capability
  being reachable on some plan, the cheapest-plan resolution per capability, and `ai_enabled=false`
  removing everything regardless of plan. Report generation (14, through the **real**
  `AiCompletionService` so the production gate is what is tested): Free refused a section and a
  whole report, Micro allowed a section but refused the whole report, Micro-Growth producing a
  persisted draft that awaits review, Micro-Growth refused the full engagement report, Small
  allowed it, a non-team member refused, a draft unreachable through another engagement,
  a Senior unable to accept, a Partner accepting with the acceptance recorded, no second review,
  regeneration leaving the reviewed version untouched, sources surviving onto the sections,
  prose from the provider still yielding a reviewable draft, and the prompts carrying the
  anti-fabrication rules. Agentic reporting (5): every plan below Medium-Growth refused,
  Medium-Growth storing a draft for review, non-team member refused, the tool set confined to one
  engagement, and the agent instructed not to fill gaps. Portfolio (6): plans below Medium
  refused, a member seeing only assigned engagements, an Admin seeing the organisation, a member
  assigned to nothing getting nothing, client scoping, and the reporting discipline applied.
  Domain (7): a draft starting awaiting-review, no empty draft, no acceptance without review
  authority, acceptance recording the reviewer, rejection requiring a reason, no double review,
  and citations surviving a round trip. Plus +3 evidence-versioning domain tests (retained history with per-version paths and
  notes, tag normalisation, empty content refused on upload and supersede), +1 activity
  predicate-form assertion, +1 recent-activity confinement test (the org feed shows only the caller's
  engagements), +5 agent workflow tests (Phase 7) and +6 linked-accounting workflow tests
  (team confinement, provenance-stamped import, entitlement propagation, unavailability
  without data leakage). Integration (12): adapter gating (dual entitlement, link flag,
  archived-entity filtering, vocabulary mapping) and link slices (Admin-only, enable
  requires both entitlements, status view).
- Accounting (90): +1 activity predicate-form assertion; +5 agent workflow tests (Phase 7); +3 read-contract tests (snapshots
  from posted lines only, cross-entity
  period rejection); 11 AI workflow tests (permission denial, `ai_enabled=false` → 402 before
  any provider call, context assembly per capability, tier tagging, cross-entity
  protection, Manage-gated close review, capability catalog per plan) plus the Phase 4
  suite — domain rules (journal balance/lifecycle/reversal, account
  hierarchy/normal balance/reclassification, period close/reopen/overlap, reconciliation
  difference rules, entity archive/currency) and workflows through the real pipeline with
  in-memory fakes (permission denials for Viewer/Member/Manager boundaries, Free-plan
  entity limit, per-period transaction limit, storage limit, closed/missing-period
  rejection, summary/inactive account rejection, posting → ledger lines + activity,
  reversal authority, trial balance/income statement/balance sheet arithmetic, general
  ledger running balances, full reconciliation flows, document upload/link/download).
- `_Tests/Ledgance.Accounting.Unit.Tests/Support/` holds `AccountingFakes` and
  `LedgerHarness` (wires all Ledger slices through `MediatorTestHarness`).

---

## What remains

1. **Live Stripe bring-up** — Phase 9 is implemented and tested against a fake provider, but no
   call has run against a real Stripe account. Create the products and prices, map them through
   `Stripe:Prices:*`, register the webhook endpoint, then walk checkout → webhook → entitlement
   change once end to end. Phase 9.5 replaced the Audit plan codes, so the three sandbox Audit
   prices created earlier no longer map to anything — five new Audit prices are needed
   (`AuditMicro`, `AuditMicroGrowth`, `AuditSmall`, `AuditMedium`, `AuditMediumGrowth`).
   Until then every paid Audit plan reports `purchasable: false`. Tax/VAT, metered pricing and
   dunning beyond the past-due state remain out of scope.
2. **AI credit prices are a first cut.** The per-capability costs and the rescaled allowances
   are reasoned estimates of relative product value, not measurements of provider spend. They
   are configuration (`Ai:OperationCosts`) and catalogue values precisely so they can be retuned
   once real usage exists; nothing in the code assumes today's numbers.
3. **`max_users` has no enforcement point.** The limit is declared per plan and surfaced in
   billing usage, but nothing in the product adds an organisation member: membership is created
   only by `ProvisionOrganizationCommand` (the owner), and engagement team assignment requires an
   existing member. The check belongs in the member-invitation slice, which does not exist yet.
   Every other Audit limit — clients, engagements, storage, AI units, AI capabilities — is
   enforced server-side today.
4. **Remaining frontend gaps** — the deep product workspaces, AI and agent UX now exist
   (see the post-Phase 8 section). Still unbuilt: the review-notes UI (`POST/resolve` note
   endpoints on working papers are implemented but unused by the frontend), reconciliation
   line-clearing (`PUT …/reconciliations/{id}/cleared-lines` likewise), and the
   accounting-link management UI — the frontend never calls
   `api/integration/accounting-link`, so an Admin/Owner currently has no way to enable
   sharing from the UI. Audit's trial-balance tab does read
   `GET api/audit/accounting-context` and import from it once the link is on. There is no
   engagement **Export** and no in-browser document **preview** — both were left out rather
   than shipped as buttons that do nothing.
5. Security review, quality, polish (Phases 10–13).
6. Shared accounting context beyond the trial balance (GL drill-down, statements,
   documents) — widen `IAccountingReadContract` + `ILinkedAccountingSource` when an Audit
   workflow needs it.
7. Write-capable agent tools with explicit human acceptance, if the product ever wants
   them — the pipeline-as-tool-boundary design (ADR-022) already supports it safely.

---

## Known issues and limitations

1. **Live verification is partial.** Proven against the live project: auth (asymmetric
   JWKS), onboarding/organization provisioning, product scoping, audit client creation,
   RLS + role grants. **Not yet walked live**: journal posting (the jsonb line mapping,
   `DateOnly`↔`date` round-trips and the per-line ledger insert loop), storage uploads
   (evidence / accounting documents), reconciliation flows, and the linked-books import.
1a. **No AI call has run against a live provider.** The full AI/agent UX is built and
   plan-gated, but completions need a provider: the basic tier routes to Ollama
   (`localhost:11434`, model `llama3.1:8b`) — install it locally for working Free-tier
   AI; paid tiers need real OpenAI/Anthropic keys in `appsettings.local.json`. The
   OpenClaw `v1/agent/turns` protocol is an assumed contract verified against fakes only.
   Accounting AI context builders fetch all ledger lines up to the period end per request
   — same in-memory aggregation posture as the reports (limitation 3).
1b. **Social sign-in requires dashboard setup** — Google and LinkedIn (OIDC) must be
   enabled under Supabase Authentication → Providers with real OAuth credentials, and
   `http://localhost:3000` allowed in URL Configuration; until then the buttons surface
   Supabase's "provider is not enabled" error as a toast.
1c. **A dead database call waits the full 100-second default HttpClient timeout** (seen
   live when a lock blocked `audit_clients`) — a tighter Supabase HTTP timeout with a
   fast 503 belongs in the Phase 10/11 hardening pass, as does the pre-existing
   `any`-typed lint debt in `frontend/util/http.ts`.
1d. **Navigation-latency pass (2026-08-11).** Every Supabase query is a network round trip
   (~100–300ms measured), so latency is governed by round-trip count, not query cost. Done:
   session cached client-side for 10 minutes (staleTime 60s for everything else, so
   revisited pages render from cache; mutations already invalidate what they change);
   paged/infinite queries keep previous rows during page and filter changes
   (`keepPreviousData`); independent reads parallelized with `Task.WhenAll` in
   `SessionController`, `GetEngagementByIdQuery`, `EngagementProgressReader` (5 → 1 round
   trip of latency) and `GetBillingOverviewQuery`; `EntitlementService`'s per-request memo
   is now a `ConcurrentDictionary` to make that safe. Migration `0009` adds the
   `custom_access_token_hook` that stamps `org_id`/`org_role` claims —
   `CurrentUserMiddleware` already prefers them — removing the per-request membership
   lookup **once the hook is enabled in the dashboard** (Authentication → Hooks →
   Customize Access Token Claims). Trade-off for Phase 10 review: with the hook on, a role
   change or member removal takes effect on the next token refresh (≤1h), not instantly;
   the claim-less fallback path is unchanged. `app/dashboard/loading.tsx` (the app's only
   route-level Suspense fallback) makes every dashboard navigation swap immediately to a
   content skeleton — without it the App Router holds the previous page until the next
   segment is ready, which reads as the click doing nothing.
1e. **The engagement list is organization-wide, not team-confined.** `GetEngagementsQuery` and
   the new `GetPaginatedEngagementsQuery` return every engagement the caller's organization
   owns; only opening one enforces team membership (`IEngagementAccessGuard`, ADR-017). A
   Viewer with `audit:engagements:read` therefore sees engagement names, clients and statuses
   for engagements they cannot open. This predates the UI round but the paged list makes it
   prominent — decide in the Phase 10 security review whether the list should be confined too.
1f. **No Stripe call has run against a live account.** The gateway, webhook verifier and stores
   are verified by unit tests against a fake provider through the real pipeline. Products and
   prices must exist in the Stripe dashboard and be mapped through `Stripe:Prices:*`; until they
   are, every paid plan reports `purchasable: false` and checkout is refused with a reason. The
   Stripe API shape is also assumed in one place: the subscription period end is read from the
   subscription **item** (`SubscriptionItem.CurrentPeriodEnd`), which is where recent API
   versions moved it.
1g. **Payment-method availability is a Stripe account setting, not code.** GCash and Maya appear
   for Philippine customers only when the account has them enabled *and* the plan's recurring
   terms support them; the code neither guarantees nor blocks any specific method.
1h. **List search matches the record name only** (`ILike` on `name`), server-side. The clients
   grid's search box is labelled accordingly; industry and contact are not searched. The
   documents tab filters **client-side** over the already-fetched list, which is fine while an
   engagement's document count is small and would need paging if it grows.
1i. **Multi-file upload is sequential and not atomic.** Each file is its own command, so a
   failure part-way through leaves the earlier files stored; the error names the failing file
   and the list refetches to show what landed. Deliberate — a partial upload is more useful
   than an all-or-nothing rollback of files that were fine — but it is not a transaction.
1j. **Evidence version history is unbounded.** Versions accumulate as jsonb on the row and
   every version's file is retained in storage; `storage_bytes` counts only the current
   version's size, so a heavily re-versioned document under-reports against the entitlement.
   Worth revisiting in Phase 10/11.
2. **Journal entry numbering is read-then-write** (`max(entry_number)+1`) without a
   concurrency guard — two simultaneous drafts could collide on the unique index; the insert
   fails rather than corrupts. Same MVP posture as AI usage metering.
3. **Trial balance, reports and entity-wide ledger queries fetch all ledger lines up to the
   period end** and aggregate in memory; document storage sums sizes by fetching all rows.
   Fine for MVP volumes (Free 300 entries/period), revisit for scale.
4. **Posting writes the entry update and its ledger lines as separate Supabase calls** — no
   transaction. A failure between them would leave a posted entry without ledger lines;
   acceptable for MVP, worth a server-side function later.
5. **No closing-entry workflow** — the balance sheet presents life-to-date P&L as a
   current-earnings line (documented in the handler); year-end close is future work.
6. **`Ledgance.Accounting.Unit.Tests` fakes duplicate `RecordingActivityRecorder`** from the
   Audit test project; promote to `Ledgance.TestInfrastructure` if a third consumer appears.
7. **`QueryableExtensions.PaginateAsync` (LINQ-to-objects) remains unused** — candidate for
   removal when nothing adopts it.
8. **`NU1903`** transitive `Microsoft.OpenApi` advisory persists; no CI; frontend `npm audit`
   untriaged.

---

## Important files

| Path | Why it matters |
| --- | --- |
| `CLAUDE.md` / `docs/project-context.md` | Rules and product intent |
| `backend/Modules/Accounting/Ledger/Ledgance.Accounting.Ledger.Domain/` | The accounting business rules |
| `backend/Modules/Accounting/Ledger/Ledgance.Accounting.Ledger.Application/EntityAccess.cs` | Entity guard every ledger slice runs through |
| `backend/Modules/Audit/Engagement/Ledgance.Audit.Engagement.Domain/` | The audit business rules |
| `backend/Modules/Audit/Engagement/Ledgance.Audit.Engagement.Application/AccountingContext/` | Audit's accounting-context boundary: CSV baseline + linked-source port |
| `backend/Integration/Ledgance.Integration.AccountingContext/` | The only assembly bridging both contexts (ADR-021) |
| `backend/Modules/Accounting/Ledger/Ledgance.Accounting.Ledger.Application/Published/` | Accounting's published read contract |
| `backend/Shared/Ledgance.Shared.Application/Billing/` | Billing ports and slices — no provider named |
| `backend/Shared/Ledgance.Shared.Infrastructure/Billing/` | The only Stripe code (ADR-023) |
| `backend/Shared/Ledgance.Shared.Infrastructure/Activity/` | Append-only activity trail (`context_id` scoping) |
| `backend/_Tests/Ledgance.Accounting.Unit.Tests/` | Accounting domain-rule and workflow test patterns |
| `supabase/migrations/` | 0001 foundation · 0002 audit core · 0003 AI usage · 0004 accounting core · 0005 accounting link · 0006 API role grants (required on newer Supabase projects, which no longer auto-grant Data API roles on `public`) · 0007 `organizations.products` for platform scoping · 0008 billing columns (`cancel_at_period_end`, `last_event_at`) and the `billing_events` idempotency table · 0009 `custom_access_token_hook` stamping `org_id`/`org_role` claims (must also be selected in the dashboard to take effect — ADR-026) · 0010 evidence `category`, `tags` and retained `version_history` (ADR-025) |

---

## Configuration expected

- **Backend** `appsettings.local.json`: real Supabase `Url`, `AnonKey`
  (`sb_publishable_…`), `ServiceRoleKey` (`sb_secret_…`); `JwtSecret` **empty** for
  asymmetric-signing projects (JWKS is derived from the URL) or the legacy secret for
  HS256 projects. Paid-AI keys remain placeholders.
- **Stripe** (`Stripe:*`, placeholders committed): `SecretKey`, `WebhookSecret`,
  `Prices:<PlanCode>` per paid plan, and the checkout/portal return URLs. `PublishableKey` is
  carried for completeness — hosted Checkout is a redirect, so the browser never needs it.
  `PaymentMethodTypes` is empty by design, leaving method availability to the Stripe account.
- **Frontend** `.env.local`: `NEXT_PUBLIC_API_URL` (`http://localhost:5253`),
  `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_ANON_KEY` (publishable key only —
  never the secret key).
- **Supabase dashboard**: migrations 0001–0010 applied in order; email confirmation off
  for local testing; Google / LinkedIn (OIDC) providers configured when social sign-in
  should work; storage buckets `audit-evidence` and `accounting-documents` exist via
  migrations; the 0009 claims hook selected under Authentication → Hooks → Customize
  Access Token (JWT) Claims to remove the per-request membership lookup (optional — the
  API works without it, one query slower per request).
- **Local AI (optional)**: Ollama with `llama3.1:8b` makes the Free/basic AI tier work
  end-to-end without any paid key.
