# Implementation Status

Phase tracker. Detail about the current implementation lives in `project-state.md`; product
intent lives in `project-context.md`.

**Last verified:** 2026-08-10, against the repository.

| # | Phase | Status |
| --- | --- | --- |
| 1 | Foundation Infrastructure | **Completed** — verified by build, tests and an API smoke test |
| 2 | Audit Core MVP | **Completed (backend)** — API smoke-tested; live-Supabase verification still outstanding |
| 3 | Audit AI | **Completed (backend)** — 113 tests passing; live-provider verification still outstanding |
| 4 | Accounting Core MVP | **Completed (backend)** — 183 tests passing; live-Supabase verification still outstanding |
| 5 | Accounting AI | **Completed (backend)** — 194 tests passing; live-provider verification still outstanding |
| 6 | Accounting ↔ Audit Integration | **Completed (backend)** — 215 tests passing; live-Supabase verification still outstanding |
| 7 | OpenClaw / Agentic AI | Not Started |
| 8 | Frontend & UI/UX | Not Started |
| 9 | Stripe & Subscriptions | Not Started |
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

## Note on phases 8–13

Some frontend surface — the marketing site, `/login`, `/signup` and the `/dashboard` shell —
pre-existed Phase 1 and remains mock-driven. That does not make Phase 8 started; the product UI,
its data wiring and its missing routes are all still to do.
