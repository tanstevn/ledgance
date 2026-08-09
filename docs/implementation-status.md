# Implementation Status

Phase tracker. Detail about the current implementation lives in `project-state.md`; product
intent lives in `project-context.md`.

**Last verified:** 2026-08-10, against the repository.

| # | Phase | Status |
| --- | --- | --- |
| 1 | Foundation Infrastructure | **Completed** — verified by build, tests and an API smoke test |
| 2 | Audit Core MVP | **Completed (backend)** — 98 tests passing; API smoke-tested; live-Supabase verification still outstanding |
| 3 | Audit AI | Not Started |
| 4 | Accounting Core MVP | Not Started |
| 5 | Accounting AI | Not Started |
| 6 | Accounting ↔ Audit Integration | Not Started |
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

## Note on phases 8–13

Some frontend surface — the marketing site, `/login`, `/signup` and the `/dashboard` shell —
pre-existed Phase 1 and remains mock-driven. That does not make Phase 8 started; the product UI,
its data wiring and its missing routes are all still to do.
