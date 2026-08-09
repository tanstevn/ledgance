# Implementation Status

Phase tracker. Detail about the current implementation lives in `project-state.md`; product
intent lives in `project-context.md`.

**Last verified:** 2026-08-10, against the repository.

| # | Phase | Status |
| --- | --- | --- |
| 1 | Foundation Infrastructure | **Completed** — verified by build, tests and an API smoke test |
| 2 | Audit Core MVP | Not Started |
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

## Phase 2 — Audit Core MVP (Not Started)

Scope per `project-context.md` §2: clients, engagements, engagement teams, audit planning,
materiality, risk assessment, audit procedures, working papers, evidence, findings, review
workflows, audit reporting, audit history and activity, plus external accounting context import
(CSV, Excel, trial balance, general ledger, financial statements, supporting documents).

Known prerequisites:

1. Organization provisioning at sign-up — without it an authenticated user has no organization
   and every request is rejected.
2. Applying and smoke-testing `supabase/migrations/0001_foundation.sql` against a real project.
3. Replacing the stubbed `Ledgance.Audit.Client.Application` handlers, which currently return
   hard-coded sample data.
4. Registering Audit permissions into `PermissionRegistry`.

---

## Note on phases 8–13

Some frontend surface — the marketing site, `/login`, `/signup` and the `/dashboard` shell —
pre-existed Phase 1 and remains mock-driven. That does not make Phase 8 started; the product UI,
its data wiring and its missing routes are all still to do.
