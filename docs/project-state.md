# Ledgance — Project State

**Where the implementation currently is.** For what the product should be, read
`project-context.md`. This document is updated at the end of every phase.

**Last verified:** 2026-08-11, after the post-Phase 8 live bring-up and product-UX round,
against the repository and a live Supabase project.

---

## Position

| | |
| --- | --- |
| Last completed phase | **Phase 8 — Frontend**, plus a post-phase live-environment bring-up and product-UX round |
| Current phase | none in progress |
| Next phase | **Phase 9 — Stripe** (not started) |

---

## Build and test status

Verified by running the commands, not assumed.

| Check | Result |
| --- | --- |
| `dotnet build backend/Ledgance.slnx` | succeeded — 0 errors, 0 C# warnings |
| `dotnet test backend/Ledgance.slnx` | **239 passed, 0 failed** (64 shared, 74 audit, 89 accounting, 12 integration) |
| API smoke test | boots clean; `api/subscriptions/plans` → 200 anonymous with the full catalog; every other product route → 401 unauthenticated; OpenAPI 200 |
| Frontend | `npx tsc --noEmit` clean · ESLint clean on all touched files (a full `next build` cannot run while `next dev` holds `.next`; the last full build passed) |
| **Live end-to-end** | signup → onboarding → organization → audit client verified against a real Supabase project (see the bring-up section) |

---

## What is implemented

### Platform (Shared)

- **Onboarding** — `POST /api/onboarding/organization` → `ProvisionOrganizationCommand`
  (Shared.Application) creates the organization + Owner membership via `IOrganizationDirectory`.
  `[AllowWithoutOrganization]` lets it run for an authenticated user with no membership.
- **Principal vs organization context** — `CurrentUserMiddleware` records an
  `AuthenticatedPrincipal` for every verified token and a full `CurrentUser` only when
  membership exists; `AuthorizationBehavior` requires full context by default.
  `GET /api/session` returns `needsOnboarding: true` for member-less users.
- **Activity trail** — `IActivityRecorder`/`IActivityReader` (Shared.Application) over an
  append-only `activity_log` table. The scope column is the product-neutral **`context_id`**
  (renamed from `engagement_id` in Phase 4 — ADR-020): the engagement in Audit, the accounting
  entity in Accounting. Every mutating Audit and Accounting handler records to it.
- **`DomainRuleException`** → HTTP 409 in `ExceptionHandlerMiddleware`.

### Audit — unchanged since Phase 3

Client feature (`Modules/Audit/Client`), Engagement module (`Modules/Audit/Engagement`, ~30
slices, team confinement per ADR-017), Audit AI module (10 proposal-only capabilities behind
the shared AI orchestrator, ADR-018). See `implementation-status.md` Phases 2–3 for the full
inventory.

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

**Application** — vertical slices in feature folders (Entities, ChartOfAccounts, Periods,
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

`AccountingAiCapabilities` catalog (10 capabilities: 3 basic, 4 advanced, 3 reasoning) in
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
(Owner-only product activation). Of the four placeholder controllers removed in Phase 4,
the AI one has been recreated with real routes; Organization/User/Client controllers
return when their phases need them.

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
the real API → `/subscribe` (the Stripe seam: plan summary + checkout call that fails
gracefully until Phase 9, with "continue on Free" always available) →
`/subscribe/success`, which shows a subscription as active only when `/api/session`
confirms it. The cross-platform recommendation (`components/cross-sell.tsx`) renders only
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
**Migrations 0001–0007 are applied to a live Supabase project** (0006 grants the Data API
roles newer projects no longer receive by default; 0007 adds `organizations.products` for
platform scoping); RLS and `*_read_own` policies verified by SQL probes.

### Tests — 239 passing

- Shared (64): 8 agent-runner tests (Phase 7: tier gate, per-turn metering, unknown and
  forbidden tool containment, step limit, OpenClaw→chat fallback, JSON tool protocol,
  unit-limit stop), 2 plan-catalog query tests and 3 organization-product tests (signup
  choice stored, unknown product rejected, Owner-only activation) on top of the Phase 1–6
  core.
- Audit (74): +1 recent-activity confinement test (the org feed shows only the caller's
  engagements), +5 agent workflow tests (Phase 7) and +6 linked-accounting workflow tests
  (team confinement, provenance-stamped import, entitlement propagation, unavailability
  without data leakage). Integration (12): adapter gating (dual entitlement, link flag,
  archived-entity filtering, vocabulary mapping) and link slices (Admin-only, enable
  requires both entitlements, status view).
- Accounting (89): +5 agent workflow tests (Phase 7); +3 read-contract tests (snapshots
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

1. **Stripe (Phase 9, next)** — implement `POST /api/billing/checkout` (the subscribe page
   already calls it and error-handles its absence), webhooks into the `subscriptions`
   table, and redirect to `/subscribe/success` (which already verifies server-side).
2. **Deep product workspaces (frontend)** — journal entry editor, ledger/report screens,
   the audit engagement file, AI and agent UX, link-management UI. Phase 8 delivered
   discovery, subscription, onboarding and API-driven entry points, not these.
3. Security review, quality, polish (Phases 10–13).
4. Shared accounting context beyond the trial balance (GL drill-down, statements,
   documents) — widen `IAccountingReadContract` + `ILinkedAccountingSource` when an Audit
   workflow needs it.
5. Write-capable agent tools with explicit human acceptance, if the product ever wants
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
| `backend/Shared/Ledgance.Shared.Infrastructure/Activity/` | Append-only activity trail (`context_id` scoping) |
| `backend/_Tests/Ledgance.Accounting.Unit.Tests/` | Accounting domain-rule and workflow test patterns |
| `supabase/migrations/` | 0001 foundation · 0002 audit core · 0003 AI usage · 0004 accounting core · 0005 accounting link · 0006 API role grants (required on newer Supabase projects, which no longer auto-grant Data API roles on `public`) |

---

## Configuration expected

- **Backend** `appsettings.local.json`: real Supabase `Url`, `AnonKey`
  (`sb_publishable_…`), `ServiceRoleKey` (`sb_secret_…`); `JwtSecret` **empty** for
  asymmetric-signing projects (JWKS is derived from the URL) or the legacy secret for
  HS256 projects. Stripe and paid-AI keys remain placeholders.
- **Frontend** `.env.local`: `NEXT_PUBLIC_API_URL` (`http://localhost:5253`),
  `NEXT_PUBLIC_SUPABASE_URL`, `NEXT_PUBLIC_SUPABASE_ANON_KEY` (publishable key only —
  never the secret key).
- **Supabase dashboard**: migrations 0001–0007 applied in order; email confirmation off
  for local testing; Google / LinkedIn (OIDC) providers configured when social sign-in
  should work; storage buckets `audit-evidence` and `accounting-documents` exist via
  migrations.
- **Local AI (optional)**: Ollama with `llama3.1:8b` makes the Free/basic AI tier work
  end-to-end without any paid key.
