# Ledgance — Project State

**Where the implementation currently is.** For what the product should be, read
`project-context.md`. This document is updated at the end of every phase.

**Last verified:** 2026-08-10, end of Phase 4, against the repository.

---

## Position

| | |
| --- | --- |
| Last completed phase | **Phase 4 — Accounting Core MVP (backend)** |
| Current phase | none in progress |
| Next phase | **Phase 5 — Accounting AI** (not started) |

---

## Build and test status

Verified by running the commands, not assumed.

| Check | Result |
| --- | --- |
| `dotnet build backend/Ledgance.slnx` | succeeded — 0 errors, 0 C# warnings |
| `dotnet test backend/Ledgance.slnx` | **183 passed, 0 failed** (51 shared, 62 audit, 70 accounting) |
| API smoke test | boots clean; every `api/accounting/*` and `api/audit/*` route → 401 unauthenticated; OpenAPI 200; unknown routes 404 |
| Frontend | untouched in Phases 2–4 (Phase 8); `npx tsc --noEmit` clean as of Phase 1 |

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

### API surface

`api/audit/*` unchanged. New `api/accounting/entities` route family: entities (CRUD +
archive + activity), `{entityId}/accounts`, `{entityId}/periods` (+close/reopen),
`{entityId}/journal-entries` (paged list, detail, draft update/delete, post, reverse),
`{entityId}/general-ledger`, `{entityId}/trial-balance`, `{entityId}/reports/*`,
`{entityId}/reconciliations` (cleared-lines, complete, cancel), `{entityId}/documents`
(multipart upload, 25 MB command limit, signed download URLs). The four empty Accounting
placeholder controllers were removed; controllers are recreated when their phases need them
(the Accounting AI controller arrives in Phase 5).

### Database

`supabase/migrations/0004_accounting_core.sql` — renames `activity_log.engagement_id` →
`context_id`, then 7 accounting tables with FKs/checks/indexes (including one-sided-amount
checks on ledger lines and unique `(entity_id, code)` / `(entity_id, entry_number)`), the
`accounting-documents` storage bucket, and org-scoped read RLS on everything.
**Migrations 0001–0004 have still never been applied to a live Supabase project.**

### Tests — 183 passing

- Shared (51) and Audit (62): unchanged, still green after the `ContextId` rename.
- Accounting (70): domain rules (journal balance/lifecycle/reversal, account
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

1. **Phase 5 — Accounting AI** (next): `ProductModule.Accounting` workloads through the
   existing orchestrator; the `Modules/Accounting/AI` projects are still anchors.
2. Accounting↔Audit integration (Phase 6 — the `LedganceAccountingContextSource` adapter
   behind Audit's `IAccountingContextSource` port), agentic AI / OpenClaw (Phase 7).
3. **Frontend (Phase 8)** — all product pages still mock-driven; no Accounting UI exists.
4. Stripe (Phase 9), security review, quality, polish (Phases 10–13).

---

## Known issues and limitations

1. **No Supabase path has ever executed against a live project** — all four migrations are
   unapplied. The Phase 4 additions most likely to need live verification: jsonb list mapping
   (`List<JournalLineDoc>`, `List<Guid>` cleared-line ids), `date` column round-trips through
   `DateOnly`↔`DateTime` mapping, and the per-line insert loop used when posting.
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
| `backend/Modules/Audit/Engagement/Ledgance.Audit.Engagement.Application/AccountingContext/` | External accounting context boundary (Phase 6 seam) |
| `backend/Shared/Ledgance.Shared.Infrastructure/Activity/` | Append-only activity trail (`context_id` scoping) |
| `backend/_Tests/Ledgance.Accounting.Unit.Tests/` | Accounting domain-rule and workflow test patterns |
| `supabase/migrations/` | 0001 foundation · 0002 audit core · 0003 AI usage · 0004 accounting core (all unapplied) |

---

## Configuration expected

Unchanged from Phase 3: Supabase keys in `appsettings.local.json` / `.env.local`; Stripe and
AI entries as placeholders. New infrastructure expectation: the `accounting-documents`
storage bucket (created by migration 0004) alongside `audit-evidence`.
