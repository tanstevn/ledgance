# Implementation Status

Phase tracker. Detail about the current implementation lives in `project-state.md`; product
intent lives in `project-context.md`.

**Last verified:** 2026-08-11, by running `dotnet build` and `dotnet test` on
`backend/Ledgance.slnx` (**275 tests passing** — 92 shared, 81 audit, 90 accounting,
12 integration), `next build` (19 routes), `npx tsc --noEmit` and ESLint in `frontend/` — all
clean — then reading the repository.

Test counts in the per-phase rows are the totals *at the end of that phase*; **275** is the
current total.

| # | Phase | Status |
| --- | --- | --- |
| 1 | Foundation Infrastructure | **Completed** — verified by build, tests and an API smoke test |
| 2 | Audit Core MVP | **Completed (backend)** — API smoke-tested; client creation since verified live, the rest of the Audit surface not |
| 3 | Audit AI | **Completed (backend)** — 113 tests passing; live-provider verification still outstanding |
| 4 | Accounting Core MVP | **Completed (backend)** — 183 tests passing; live-Supabase verification still outstanding (journal posting and storage not yet walked live) |
| 5 | Accounting AI | **Completed (backend)** — 194 tests passing; live-provider verification still outstanding |
| 6 | Accounting ↔ Audit Integration | **Completed (backend)** — 215 tests passing; live-Supabase verification still outstanding |
| 7 | OpenClaw / Agentic AI | **Completed (backend)** — 233 tests passing; live-provider verification still outstanding |
| 8 | Frontend & UI/UX | **Completed** — discovery, subscription and onboarding flows, plus the deep product workspaces, AI/agent surfaces and auth redesign delivered in the post-phase round below |
| — | Post-Phase 8 live bring-up & product UX | **Completed** — first live Supabase run (four defects fixed), product workspaces, AI/agent UI, activity feeds, platform scoping, auth redesign |
| 9 | Stripe & Subscriptions | **Completed** — 261 tests passing; verified against a fake provider, never against a live Stripe account |
| — | Post-Phase 9 product & performance rounds | **Completed** — live Stripe prices, two billing defects fixed, activity phrasing, engagement file restructure, documents versioning UI, navigation latency |
| 10 | Security & Authorization Review | Not Started |
| 11 | Testing & Quality | Not Started |
| 12 | Product Polish | Not Started |
| 13 | Final MVP Review | Not Started |

---

## Phase 1 — Foundation Infrastructure (Completed)

Delivered:

- **Identity and organization context** — Supabase Auth JWT validation (symmetric secret or
  JWKS), `CurrentUserMiddleware` resolving `CurrentUser` from verified claims plus
  `organization_members`, `OrganizationRole`, and a startup-populated `PermissionRegistry`
  modules extend.
- **Server-side authorization** — a fallback policy requiring authentication on every endpoint,
  plus a default-deny `AuthorizationBehavior` enforcing `[RequiresPermission]`.
- **Organization isolation** — three layers: the authorization behavior, `SupabaseRepository`
  filtering/stamping/guarding every `IOrganizationOwned` model, and row-level security in
  `supabase/migrations/0001_foundation.sql`.
- **Supabase data access** — official Supabase C# client, no EF Core; a reusable tenant-scoped
  repository that still exposes the native query builder.
- **Entitlement foundation** — all nine plans in one `SubscriptionPlanCatalog`, resolution
  through catalogue → configuration → per-organization overrides, capability gating via
  `[RequiresEntitlement]` and limit checks via `EntitlementSet`, surfaced as HTTP 402.
- **Shared application foundation** — four pipeline behaviors (logging, authorization,
  entitlement, validation) on the existing custom Mediator; extended error handling; scoped CORS;
  `GET /api/session`.
- **Test foundation** — `Ledgance.TestInfrastructure` with `MediatorTestHarness` and fakes;
  41 passing tests; no test requires real credentials.
- **Configuration** — placeholder-only committed settings; real values in git-ignored
  `appsettings.local.json` and `.env.local`.
- **Frontend foundation** — Supabase Auth replacing the previous stub, bearer-token API layer,
  `QueryClientProvider`, `useSession`, `next-themes`.

Not delivered by Phase 1, by design: any Audit or Accounting business capability, Stripe, and
any AI code.

---

## Phase 2 — Audit Core MVP (Completed, backend)

Delivered: organization onboarding; activity trail; Audit Client feature with real domain,
persistence and entitlement limits; the new Engagement module covering engagements, teams,
planning, materiality, risks, procedures, working papers with preparer/reviewer segregation,
review notes, evidence with versioning and Supabase Storage, findings, audit reports, and
CSV trial-balance import behind the Audit-owned `IAccountingContextSource` boundary;
team-confinement authorization (ADR-017); `api/audit/*` API surface; migration
`0002_audit_core.sql`; 57 Audit tests + 3 new Shared onboarding tests.

Deliberately not included: any frontend work (Phase 8), the Ledgance Accounting adapter for
accounting context (Phase 6), AI (Phase 3), Stripe (Phase 9).

Outstanding risk: no Supabase call has ever run against a live project — migrations are
unapplied and the persistence/storage layer is verified by unit tests and compilation only.

---

## Phase 3 — Audit AI (Completed, backend)

Delivered: provider-agnostic AI abstractions (`AiWorkload`, `IAiCompletionService`,
`IAiChatClient`, `IAiModelRouter`, `IAiUsageMeter`) in Shared.Application; the orchestrator
enforcing authorization → tier → monthly units → context-size → routing → usage recording, with
downward-only provider fallback (402 above-tier, 503 total failure); Ollama and OpenAI HTTP
adapters and an Anthropic adapter on the official C# SDK; tier→model routing with
`Ai:Routing` configuration override; the `ai_usage` table (migration 0003); the
`AuditAiCapabilities` catalog (10 capabilities across basic/advanced/reasoning tiers); 10 Audit
AI slices — assistant/Q&A, document summarization, risk suggestions, procedure suggestions,
working-paper drafting, finding drafting, risk analysis, anomaly detection, review assistance,
report drafting — all team-confined, activity-logged and proposal-only; `api/audit/ai/*`
endpoints incl. a capability listing with per-plan `included` flags; 15 new tests (orchestrator
gating/fallback/truncation + slice authorization and workflows).

Deliberately not included: Accounting AI (Phase 5), agentic/OpenClaw (Phase 7 — the `agentic`
tier routes to Anthropic until then), evidence binary content extraction, frontend AI UX
(Phase 8).

Outstanding risk: no AI call has run against a live provider; adapters are verified by
compilation and unit tests against fakes.

## Phase 4 — Accounting Core MVP (Completed, backend)

Delivered: the `Modules/Accounting/Ledger` module (ADR-019) — accounting entities with
fixed base currency and archive rules; hierarchical typed chart of accounts with normal
balances and summary-account posting rules; fiscal periods with draft-blocking close and
reopen; balanced double-entry journal with Draft → Posted → Reversed lifecycle where
posting materializes append-only ledger lines and corrections happen only by reversal;
derived general ledger, trial balance, income statement and balance sheet; account
reconciliation with cleared lines and explained differences; source documents in the
`accounting-documents` bucket; entity-scoped activity history over the generalized
`context_id` activity column (ADR-020); `api/accounting/entities/*` API surface;
entitlement enforcement (`max_entities`, `max_transactions_per_period`, `storage_bytes`);
`accounting:ledger:*` permissions; migration `0004_accounting_core.sql`; 70 Accounting
tests.

Deliberately not included: Accounting AI (Phase 5), the Ledgance Accounting adapter for
Audit's accounting context (Phase 6), closing entries / year-end close, frontend (Phase 8),
Stripe (Phase 9).

Outstanding risk: no Supabase call has ever run against a live project; posting writes the
entry and its ledger lines without a transaction; entry numbering is read-then-write behind
a unique index.

## Phase 5 — Accounting AI (Completed, backend)

Delivered: the `AccountingAiCapabilities` catalog (10 capabilities across
basic/advanced/reasoning tiers) in `Modules/Accounting/AI`; 10 proposal-only slices riding
the Phase 3 orchestrator unchanged — assistant/entity Q&A, journal-entry explanation,
period financial summary, journal-entry suggestion from a described transaction,
reconciliation assistance, statement explanation, two-period variance analysis, anomaly
detection, complex financial analysis, and a Manage-gated period-close review; context
assembled by `LedgerAiContext` exclusively from the Ledger module's repositories
(entity-guarded, activity-logged, metered under `ProductModule.Accounting`);
`api/accounting/ai/*` endpoints incl. a capability listing with per-plan `included` flags;
11 new tests (permission and entitlement gating incl. `ai_enabled=false` → 402 before any
provider call, context assembly, tier tagging, cross-entity protection, catalog per plan).

Deliberately not included: agentic/OpenClaw (Phase 7), AI acceptance-into-the-books flows
beyond the existing manual commands, frontend AI UX (Phase 8).

Outstanding risk: no AI call has run against a live provider; slices are verified against
fakes.

## Phase 6 — Accounting ↔ Audit Integration (Completed, backend)

Delivered: the `Ledgance.Integration.AccountingContext` assembly (ADR-021) bridging the two
contexts without any cross-context reference — Accounting's published
`IAccountingReadContract` (entity/period/trial-balance snapshots from posted ledger lines
only), Audit's `ILinkedAccountingSource` port in Audit vocabulary, and the adapter that
re-verifies the `accounting_context_sharing` entitlement on both products plus the
Admin-managed per-organization link on every call; link management slices and
`integration:accounting_link:*` permissions; migration `0005_accounting_link.sql`; Audit
slices to browse linked context and import a provenance-stamped trial balance
(`TrialBalanceSource.LedganceAccounting`); `api/integration/accounting-link` and
`api/audit/accounting-context` / `…/trial-balance/from-accounting` endpoints; the CSV
external source unchanged as the baseline; 21 new tests across Accounting (read contract),
Audit (import workflows) and the new `Ledgance.Integration.Unit.Tests` (adapter gating,
link slices).

Deliberately not included: sharing beyond the trial balance (GL drill-down, statements,
documents — the contract widens when an Audit workflow needs it), frontend link/import UI
(Phase 8).

Outstanding risk: no Supabase call has ever run against a live project; the link table and
adapter are verified by unit tests only.

## Phase 7 — OpenClaw / Agentic AI (Completed, backend)

Delivered: the agentic layer (ADR-022) — `IAgentRunner`/`AgentWorkload`/`AgentTool`
contracts in Shared.Application and `AgentRunnerService` in Shared.Infrastructure enforcing
the `agentic` tier gate, one usage unit per provider turn with per-turn re-checks, a
tool-step cap with a forced no-tools final turn, and containment of tool failures
(authorization, entitlement and domain-rule denials become tool results, never bypasses);
the `OpenClawAgentClient` (native `v1/agent/turns` adapter — OpenClaw chooses tools,
execution never leaves the application) with downward fallback driving the same loop over
chat providers via `ChatAgentAdapter`'s strict-JSON protocol; `audit.agent` and
`accounting.agent` capabilities whose tools are whitelists of read-only mediator requests
re-entering the full pipeline with the caller's identity and a server-fixed
engagement/entity scope; `POST api/audit/ai/engagements/{id}/agent` and
`POST api/accounting/ai/entities/{id}/agent` returning proposal-only `AgentRunReport`s with
full step transcripts; activity logging (`ai.agent`); 18 new tests (tier gating, per-turn
metering, unknown/forbidden tool containment, step limit, OpenClaw fallback, JSON-protocol
tool driving, team/entity/permission boundaries, catalog inclusion per plan).

Deliberately not included: write-capable agent tools (material changes still go through the
normal human commands), multi-agent orchestration, frontend agent UX (Phase 8).

Outstanding risk: no AI call has run against a live provider; the OpenClaw protocol is
verified against fakes only.

## Phase 8 — Frontend (Completed)

The phase itself delivered the discovery/subscription scope described below; the product
workspaces it deferred were built immediately afterwards in the post-phase round (next section),
so the frontend is no longer limited to entry points.

Delivered: a two-platform marketing site grounded in the implemented capabilities — landing
page with platform chooser (Accounting vs Audit, independent by design), `/accounting` and
`/audit` product pages, `/pricing` with per-platform tabs driven by the new anonymous
`GET /api/subscriptions/plans` endpoint (plan features derive from the same
`SubscriptionPlanCatalog` the backend enforces; Solo remains the only priced plan per the
product docs, Enterprise is Contact Sales); fabricated landing content (invented stats,
testimonials, SOC 2 claims, made-up prices) removed. Flows: platform/plan-aware signup →
real onboarding (`POST /api/onboarding/organization`) → subscribe page (the Stripe seam:
plan summary, checkout call that fails gracefully until Phase 9) → `subscribe/success`
which trusts only the server-confirmed session. Cross-platform recommendation
(`CrossSellCard`): appears only after a backend-confirmed qualifying paid plan
(Solo→Professional range; never Free, never Enterprise, never during initial discovery),
always optional with explore / maybe later / dismiss. Dashboard reworked onto real APIs:
session-driven layout with onboarding gate, plan badges and org name (SessionResponse now
carries `organizationName`), overview with live entity/client counts, Accounting entities
page and Audit clients page (list + create with loading/empty/error/populated states),
plans & billing page; `lib/mock-data.ts` deleted. Env files (`.env`, `.env.development`,
`.env.local`) carry sample values only. Validation: `next build` (16 routes) and
`tsc --noEmit` clean; backend 235 tests passing (+2 for the plans query).

Deliberately not included in the phase itself: Stripe checkout (Phase 9 — the frontend flow is
built up to the checkout boundary) and payment management UI.

## Phase 9 — Stripe, subscriptions and billing (Completed)

Delivered (ADR-023): the billing layer behind provider-agnostic ports in
`Shared.Application/Billing` — `IBillingGateway`, `IBillingPriceCatalog`,
`IBillingWebhookVerifier`, `ISubscriptionStore`, `IProcessedEventStore` — with the only Stripe
code in `Shared.Infrastructure/Billing` (`Stripe.net` 52.3.0). Slices: start checkout, open the
billing portal, change plan, cancel or resume, billing overview, and webhook handling. Free and
Enterprise never reach checkout; a plan with no configured price is refused and reported as not
purchasable, which the public plans endpoint now exposes so the UI cannot offer a purchase the
server would decline. Checkout carries organisation/module/plan metadata onto the subscription,
so every later event is matched without trusting request input; the plan is then read from the
billed price, keeping provider-portal changes in sync. Webhooks
(`POST /api/billing/webhook`, anonymous, signature-verified) are idempotent through
`billing_events` and discard events older than the row's `last_event_at`. Payment methods come
from the Stripe account's own configuration — cards and wallets internationally, GCash and Maya
for Philippine customers where the account and the plan's recurring terms support them — with
`Stripe:PaymentMethodTypes` available to pin the list. Migration `0008_billing.sql` adds
`cancel_at_period_end`, `last_event_at`, the lookup indexes and the `billing_events` table
(RLS on, no policies). Frontend: the billing page manages the real subscription (status,
renewal or end date, past-due warning, plan picker for upgrade/downgrade, payment methods and
invoices via the portal, cancel and resume, owner-only), `/subscribe` runs real checkout, and a
402 from any endpoint raises one upgrade offer through `UpgradePrompt`. 17 new tests.

Deliberately not included: tax/VAT configuration, invoicing beyond Stripe's own portal, seat- or
usage-metered pricing, and dunning beyond the past-due state the UI surfaces.

Outstanding risk: **no call has run against a live Stripe account.** Products and prices must be
created in the Stripe dashboard and mapped through `Stripe:Prices:*`; the webhook endpoint must
be registered with the events listed in `subscription-entitlements.md` §5. Everything is
verified against a fake provider through the real pipeline.

## Post-Phase 8 — live bring-up and product UX (Completed)

Not a numbered phase; work done directly after Phase 8 and recorded here so the tracker matches
the repository.

Delivered:

- **First live Supabase run**, which surfaced and fixed four defects: JWKS-based validation for
  asymmetric projects (no OIDC discovery document exists), explicit Data API role grants
  (migration `0006`), client-generated primary keys actually sent on insert (all 24 persistence
  models — every `[PrimaryKey("id", false)]` corrected), and global `JsonStringEnumConverter`
  registration. Verified live: signup → onboarding → organization + Owner membership → audit
  client creation, with RLS and grants confirmed by SQL probes.
- **Deep product workspaces.** Audit: engagements list/create and a per-engagement workspace
  (`/dashboard/audit/engagements/{id}`) with planning, fieldwork, delivery and AI tab groups —
  plan and materiality editing, plan approval, status transitions, team assignment, risks,
  procedures, working papers with ordered sign-offs, versioned evidence, trial-balance import
  (CSV and linked books), findings, the audit report and the activity trail. Accounting: a
  per-entity workspace (`/dashboard/accounting/{entityId}`) with chart of accounts, fiscal
  periods, a balanced multi-line journal editor with post/reverse/delete, live trial balance and
  statements, reconciliations, document upload, activity and AI.
- **AI and agent UX** on both platforms — a standalone assistant page per product, and a
  per-workspace AI tab exposing the ten scope-bound capabilities (everything except the
  standalone assistant) as tool cards, including the agent with its tool-trail display. Output
  renders as proposal cards carrying provider/model/tier provenance; locked capabilities are
  annotated from the server's `included` flags, never from client logic.
- **Org-wide activity feeds** — `GET /api/audit/activity` (team-confined per ADR-017, covered by
  a test) and `GET /api/accounting/activity`, plus a platform-aware dashboard overview with stat
  cards and deadline tracking.
- **Platform scoping** — the signup platform choice persisted on `organizations.products`
  (migration `0007`), returned by `/api/session`, and used to scope navigation; the Owner can
  activate the other platform free via `POST /api/organization/products`.
- **Auth redesign** — Google and LinkedIn (OIDC) social sign-in, inline forgot-password flow,
  platform/plan intent carried through the OAuth redirect.
- **List and engagement-file UI round (2026-08-11)** — card grids with infinite scroll for
  Audit clients and Accounting entities (10 per fetch, five per row), numbered pagination with
  status and client filters for engagements (10 per page), and a rebuilt engagement file
  header, stat tiles, stage-gate band and overview. Backed by new server-side paging:
  `GetPaginatedEngagementsQuery`, `GetPaginatedEntitiesQuery`, engagement counts on the paged
  client rows, and the repository/port methods they need. +5 tests (244 total).

Deliberately not included: review-notes UI and reconciliation line-clearing UI (the backend
endpoints exist and are unused by the frontend), and Stripe (Phase 9).

Outstanding risk: live verification stops after audit client creation — journal posting, storage
uploads, reconciliation and the linked-books import have not been exercised against the live
project, and no AI call has run against a live provider.

---

## Post-Phase 9 — product and performance rounds (Completed)

Not a numbered phase; work done directly after Phase 9, recorded here so the tracker matches
the repository.

**Billing follow-ups**

- **Live prices from Stripe.** `IBillingPriceReader`/`StripePriceReader` reads
  `unit_amount`, currency and interval for each configured price (cached 5 minutes, degrading
  to "no price" on any provider error so the anonymous pricing page cannot be taken down by a
  billing outage). `GET /api/subscriptions/plans` returns them and `priceLabel()` formats
  every pricing surface, so displayed prices cannot drift from the invoice. Hardcoded labels
  survive only as fallbacks.
- **Price-id validation** — a `Stripe:Prices:*` value that is not a `price_…` identifier (an
  amount typed by mistake, a `prod_…` id) is rejected at startup with a warning naming the key,
  and the plan reports as not purchasable instead of failing at checkout.
- **Two defects found by the first live checkout.** `entitlement_overrides` was inserted as SQL
  null — an insert serialises every property, so a null collection never falls back to the
  column default; every shared persistence model now initialises its collections, guarded by a
  reflection test. And the Stripe customer was persisted *after* the checkout session was
  created, so a failure in between orphaned it; the customer is now stored first and a retry
  reuses it.
- **Plan picker** rebuilt as a horizontal carousel with edge chevrons.

**Product**

- **Activity phrasing (ADR-024).** All 71 recorded summaries rewritten as active-voice
  predicates so every feed renders one sentence; `lib/activity.ts` composes actor + predicate
  for the dashboard feed and both Activity tabs.
- **Engagement file.** Four primary tabs (Overview, Documents, Working Papers, Team) in a
  segmented bar with counts, everything else behind a **More** menu; header with partner and
  manager, an **Edit** dialog, and a circled chevron beside the status opening a stage menu;
  four stat tiles; the overview reduced to working-paper sign-off status and the
  needs-attention list.
- **Documents (ADR-025, migration `0010`).** Evidence keeps a retained version history with
  per-version download, auto-versions on a repeated file name, and carries a category and
  tags. The tab is a card grid (up to 4 per row) with search and category chips; a card opens
  a details modal whose **Upload new version** button widens it into two balanced columns —
  details left, single-file upload panel right, link icon on the divider. Uploads on both
  platforms accept multiple files, each its own command through the full pipeline.
- **Accounting documents** gained the same button-opened upload modal and drop zone.

**Performance**

- **Navigation-latency round (2026-08-11)** — every Supabase query is a ~100–300ms network
  round trip, so the pass reduced round-trip counts: 10-minute client-side session cache
  (the gate every dashboard page waits on), 60s default staleTime, `keepPreviousData` on
  paged/infinite lists; `Task.WhenAll` in the session endpoint, engagement detail, progress
  reader and billing overview (engagement detail ~11 → ~5 sequential round trips); and
  migration `0009` — a `custom_access_token_hook` stamping `org_id`/`org_role` claims so the
  per-request membership lookup disappears once the hook is enabled in the dashboard.
  Claim staleness on role change (≤1h) is flagged for the Phase 10 security review.
- **Route-level `loading.tsx`** for the dashboard: without it the App Router held the previous
  page until the next segment was ready, so a click appeared to do nothing.

**Validation.** Backend build clean; **275 tests** (+14 this round: billing price catalog and
live-price rows, persistence-model null guard, checkout-orphan retry, evidence versioning and
tag normalisation, activity predicate form). `next build` 19 routes, `tsc` and ESLint clean.

Outstanding risk unchanged: no checkout has completed against a live Stripe account, no AI call
has run against a live provider, and live Supabase verification still stops after audit client
creation.
