# Ledgance — Project State

**Where the implementation currently is.** For what the product should be, read
`project-context.md`. This document is updated at the end of every phase.

**Last verified:** 2026-08-10, end of Phase 2, against the repository.

---

## Position

| | |
| --- | --- |
| Last completed phase | **Phase 3 — Audit AI (backend)** |
| Current phase | none in progress |
| Next phase | **Phase 4 — Accounting Core MVP** (not started) |

---

## Build and test status

Verified by running the commands, not assumed.

| Check | Result |
| --- | --- |
| `dotnet build backend/Ledgance.slnx` | succeeded — 0 errors, 0 C# warnings |
| `dotnet test backend/Ledgance.slnx` | **113 passed, 0 failed** (51 shared, 62 audit, 0 accounting) |
| API smoke test | boots clean; every `api/audit/*` (incl. `api/audit/ai/*`), `api/session` and `api/onboarding/*` route → 401 unauthenticated; OpenAPI 200; unknown routes 404 |
| Frontend | untouched in Phases 2–3 (Phase 8); `npx tsc --noEmit` still clean |

---

## What is implemented

### Platform (Shared) — added in Phase 2

- **Onboarding** — `POST /api/onboarding/organization` → `ProvisionOrganizationCommand`
  (Shared.Application) creates the organization + Owner membership via `IOrganizationDirectory`.
  `[AllowWithoutOrganization]` lets it run for an authenticated user with no membership.
- **Principal vs organization context** — `CurrentUserMiddleware` records an
  `AuthenticatedPrincipal` for every verified token and a full `CurrentUser` only when
  membership exists; `AuthorizationBehavior` requires full context by default.
  `GET /api/session` returns `needsOnboarding: true` for member-less users.
- **Activity trail** — `IActivityRecorder`/`IActivityReader` (Shared.Application) over an
  append-only `activity_log` table; every mutating Audit handler records to it.
- **`DomainRuleException`** → HTTP 409 in `ExceptionHandlerMiddleware`.
- `organization_members` now carries `display_name` / `email` for team display.

### Audit — Client feature (`Modules/Audit/Client`)

`AuditClient` domain entity (create/update guards; archive blocked while active engagements
exist; clients are never deleted — ADR in `decisions.md`). Slices: create (enforces
`max_clients`), update, archive, list, get-by-id, paged list (Supabase `Range`/`Count`
server-side). Infrastructure project with `audit_clients` persistence. Permissions:
`audit:clients:read` (Viewer+), `audit:clients:manage` (Manager+).

### Audit — Engagement feature (`Modules/Audit/Engagement`) — new module

**Domain** (`Ledgance.Audit.Engagement.Domain`), the rule-bearing core:

- `Engagement` — lifecycle `Planning → Fieldwork → Review → SignedOff → Completed` with gates:
  fieldwork needs an approved plan + materiality; review needs all procedures concluded;
  sign-off needs team-Partner role, no open procedures/papers/notes/findings and no High risk
  without a responsive procedure; completion needs a finalized report. Signed-off engagements
  are immutable. Editing an approved plan withdraws approval.
- `Materiality` — performance < overall, clearly-trivial < performance, all positive, basis +
  rationale required.
- `Risk` — likelihood × impact → Low/Medium/High (score ≥6 High, ≥3 Medium).
- `AuditProcedure` — Planned → InProgress → Completed (conclusion required) / NotApplicable
  (justification required).
- `WorkingPaper` — Draft → Prepared → Reviewed → Approved; preparer ≠ reviewer/approver;
  approval needs team Manager/Partner and no open review notes; editing withdraws sign-offs;
  approved papers immutable. `ReviewNote` open/resolve lifecycle.
- `Evidence` — metadata + versioning (`Supersede` keeps identity, bumps version).
- `Finding` — Open → Resolved/RiskAccepted → Closed, with mandatory notes/justifications.
- `AuditReport` — draft/finalize; only team Partner finalizes; open findings block; modified
  opinions require a basis; finalized reports immutable.
- `TrialBalanceImport` — totals computed, out-of-balance kept but flagged.

**Application** — ~30 slices in feature folders (Engagements, Team, Planning, Fieldwork,
WorkingPapers, Evidence, Findings, Reporting, AccountingContext, Activity), each colocating
command/validator/handler. `IEngagementAccessGuard` enforces team confinement (ADR-017).
`CreateEngagement` enforces `max_engagements` and auto-assigns the creator as Partner.
`UploadEvidence` enforces `storage_bytes`. Permissions: `audit:engagements:read` (Viewer+),
`contribute` (Member+), `manage`/`approve` (Manager+).

**External accounting context** — `IAccountingContextSource` port owned by Audit;
`CsvAccountingContextSource` (quoted-CSV parser, header detection) is the baseline
implementation. Phase 6 adds the Ledgance Accounting adapter behind the same port.

**Infrastructure** — persistence models for 9 tables (plan/materiality/notes/TB lines as
jsonb), repositories composing `SupabaseRepository<T>`, `EngagementProgressReader` (computes
stage-gate snapshot), `ClientLookup`/`ClientEngagementCounter` (cross-feature ports),
`SupabaseEvidenceFileStore` (private `audit-evidence` bucket, signed URLs).

### AI (Phase 3)

- **Shared AI foundation** — `Shared.Application/Ai` contracts (`AiWorkload`,
  `IAiCompletionService`, `IAiChatClient`, `IAiModelRouter`, `IAiUsageMeter`) and the
  `Shared.Infrastructure/Ai` orchestrator: authorization → `ai_enabled` → tier gate → monthly
  units (`ai_usage` table, migration 0003) → per-document context truncation + token gate →
  tier-routed execution → usage recording on success only. Downward-only fallback; above-tier =
  402; all-providers-failed = 503.
- **Providers** — Ollama (`api/chat`) and OpenAI (`v1/chat/completions`) over HttpClient;
  Anthropic via the official `Anthropic` SDK (default `claude-opus-5` for reasoning/agentic).
  Routing overridable per tier via `Ai:Routing` configuration.
- **Audit AI module** — `AuditAiCapabilities` catalog (10 capabilities: 2 basic, 4 advanced,
  4 reasoning) in AI.Domain; 10 slices in AI.Application, each team-confined via
  `IEngagementAccessGuard`, context-assembled from the caller's own repositories
  (`EngagementAiContext`), activity-logged (`ai.*`), returning `AiProposalResult` — proposals
  only, no write path into the audit record. `api/audit/ai/*` endpoints plus
  `GET api/audit/ai/capabilities` with per-plan `included` flags.
- The `Audit.AI.Infrastructure` project remains empty (context assembly needed no
  infrastructure); `OpenClaw` config keys remain unread until Phase 7.

### API surface

`api/audit/*` route convention. Controllers: clients, engagements (core/team/plan/materiality/
status/activity), fieldwork (risks/procedures/trial-balance), working papers (+notes), evidence
(multipart upload, 25 MB command limit), findings + report, users (org member list),
onboarding, session. The empty Accounting controllers and default routes remain untouched for
Phase 4; `AuditAIController`/`AuditOrganizationController` placeholders were removed (recreated
when their phases need them).

### Database

`supabase/migrations/0002_audit_core.sql` — member profile columns, `activity_log`, 9 audit
tables with FKs/checks/indexes, the `audit-evidence` storage bucket, and org-scoped read RLS on
everything. **Still never applied to a live Supabase project.**

### Tests — 98 passing

- Shared (41): prior foundation coverage + onboarding (provision, already-member rejection,
  unauthenticated rejection).
- Audit (57): domain rules (engagement lifecycle/gates, materiality, working-paper
  segregation-of-duties, findings, report finalization, trial balance, team rules, risk
  levels) and workflows through the real pipeline with in-memory fakes (permission denials,
  Free-plan client/engagement limits, client-existence check, creator-as-Partner, team
  confinement incl. Admin oversight and Admin-cannot-sign-off, CSV parsing).
- `_Tests/Ledgance.Audit.Unit.Tests/Support/AuditFakes.cs` holds reusable in-memory fakes.

---

## What remains

1. **Phase 4 — Accounting Core MVP** (next).
2. Accounting AI (Phase 5), Accounting↔Audit integration (Phase 6 — the
   `LedganceAccountingContextSource` adapter), agentic AI / OpenClaw (Phase 7).
3. **Frontend (Phase 8)** — all product pages still mock-driven, dashboard sub-routes still
   404, no onboarding UI, no AI UX. The frontend has no knowledge of the Phase 2–3 API.
4. Stripe (Phase 9), security review, quality, polish.

---

## Known issues and limitations

1. **No Supabase path has ever executed against a live project** — now including all Phase 2
   persistence, storage upload/signed URLs, jsonb round-trips and the three migrations. Apply
   migrations and smoke-test before trusting them; jsonb list mapping (`List<Guid>`,
   `List<ReviewNoteDoc>`) via the Supabase client is the most likely friction point.
1a. **No AI call has run against a live provider.** The Ollama/OpenAI adapters and the
   Anthropic SDK adapter compile and pass unit tests against fakes; response-shape assumptions
   are unverified against real services. Evidence summarization uses metadata/description only
   (no binary content extraction). AI usage metering is read-then-write without a concurrency
   guard — adequate for MVP volumes.
2. **`GetEngagementsQuery` lists all org engagements to any `engagements:read` holder** (names
   and statuses only); content behind detail endpoints is team-confined. Tighten in Phase 10 if
   list visibility should also be team-scoped.
3. **Evidence storage sums sizes by fetching all rows** and TB imports store lines as one jsonb
   document — fine for MVP volumes, revisit for scale.
4. **No engagement-scoped list pagination** (risks/procedures/papers/findings return full
   lists) — acceptable at engagement scale.
5. **`Ledgance.Accounting.Unit.Tests` still has no tests**; Accounting modules remain anchors.
6. **`QueryableExtensions.PaginateAsync` (LINQ-to-objects) is now unused** by real code —
   candidate for removal when nothing else adopts it.
7. **`NU1903`** transitive `Microsoft.OpenApi` advisory persists; no CI; frontend `npm audit`
   untriaged.
8. Route convention settled as `api/audit/...` — the old `api/audit-client` route is gone
   (breaking change; frontend never called it).

---

## Important files

| Path | Why it matters |
| --- | --- |
| `CLAUDE.md` / `docs/project-context.md` | Rules and product intent |
| `backend/Modules/Audit/Engagement/Ledgance.Audit.Engagement.Domain/` | The audit business rules |
| `backend/Modules/Audit/Engagement/Ledgance.Audit.Engagement.Application/EngagementAccess.cs` | Team confinement guard |
| `backend/Modules/Audit/Engagement/Ledgance.Audit.Engagement.Application/AccountingContext/` | External accounting context boundary (Phase 6 seam) |
| `backend/Shared/Ledgance.Shared.Application/Onboarding/` + `Shared.Infrastructure/Onboarding/` | Sign-up → organization flow |
| `backend/Shared/Ledgance.Shared.Infrastructure/Activity/` | Append-only activity trail |
| `backend/_Tests/Ledgance.Audit.Unit.Tests/` | Domain-rule and workflow test patterns |
| `supabase/migrations/` | 0001 foundation + 0002 audit core (both unapplied) |

---

## Configuration expected

Unchanged from Phase 1: Supabase keys in `appsettings.local.json` / `.env.local`; Stripe and AI
entries remain unread placeholders. New infrastructure expectation: the `audit-evidence` storage
bucket (created by migration 0002).
