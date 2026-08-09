# Implementation Status

**Last updated:** 2026-08-10 — end of Phase 1 (Foundation & Shared Infrastructure)

## Legend

`—` not started · `◐` scaffolded / partial · `●` implemented

---

## Phase progress

| # | Phase | Status |
| --- | --- | --- |
| 0 | Repository Discovery & Architecture | ● complete |
| 1 | Foundation & Shared Infrastructure | ● complete |
| 2 | Audit Core MVP | — |
| 3 | Audit AI | — |
| 4 | Accounting Core MVP | — |
| 5 | Accounting AI | — |
| 6 | Accounting ↔ Audit Integration | — |
| 7 | OpenClaw / Agentic AI | — |
| 8 | Frontend & UI/UX | ◐ marketing + shell exist; product pages still mock-driven |
| 9 | Stripe & Subscription Finalization | ◐ entitlement foundation done; Stripe not started |
| 10 | Security & Authorization | ◐ foundation done; product permissions pending |
| 11 | Testing & Quality | ◐ harness + 41 tests over the foundation |
| 12 | Product Polish | — |
| 13 | Final MVP Review | — |

---

## Verification

| Check | Result |
| --- | --- |
| `dotnet build Ledgance.slnx` | succeeded, 0 errors, 0 C# warnings |
| `dotnet test Ledgance.slnx` | 41 passed, 0 failed (38 shared, 3 audit) |
| `npx tsc --noEmit` | clean |
| `npm run build` | compiled successfully, lint clean |

---

## Backend

25 projects on `net10.0`.

### Shared.Application

| Component | Status |
| --- | --- |
| Mediator abstractions | ● |
| `Result<T>` / `PaginatedResult<T>` | ● |
| `QueryableExtensions` | ● (LINQ-to-objects; no Supabase counterpart yet) |
| `CurrentUser`, `ICurrentUserAccessor`, `ICurrentUserInitializer` | ● |
| `OrganizationRole`, `PermissionRegistry`, `SharedPermissions` | ● |
| `[AllowAnonymousRequest]`, `[RequiresPermission]` | ● |
| `UnauthenticatedException`, `ForbiddenException`, `EntitlementException` | ● |
| `ProductModule`, `PlanCode`, `Entitlements`, `AiTiers` | ● |
| `EntitlementSet`, `IEntitlementService`, `ISubscriptionReader` | ● |
| `SubscriptionPlanCatalog` (all 9 plans) | ● |
| `[RequiresEntitlement]` | ● |

### Shared.Infrastructure

| Component | Status |
| --- | --- |
| Mediator implementation | ● executor cache added; duplicate `IMediator` registration and the `[PipelineOrder]` NRE fixed |
| `LoggingBehavior` (0) | ● |
| `AuthorizationBehavior` (100) | ● default-deny + permission checks |
| `EntitlementBehavior` (200) | ● capability checks |
| `ValidationBehavior` (300) | ● FluentValidation |
| `SupabaseSettings` + client registration | ● service-role singleton, best-effort init |
| `SupabaseRepository<TModel>` | ● tenant-scoped CRUD + scoped query builder |
| `IEntityModel`, `IOrganizationOwned`, `TenantScope` | ● |
| Persistence models (organization, member, subscription) | ● |
| `CurrentUserContext`, `CurrentUserMiddleware` | ● |
| `IOrganizationMembershipReader` | ● Supabase-backed |
| `SupabaseSubscriptionReader`, `EntitlementService` | ● |
| `AddSupabaseAuthentication` (JWT: symmetric secret or JWKS) | ● |
| `AddLedganceSharedInfrastructure` | ● |
| Storage abstraction | — Phase 2 |
| AI abstractions | — Phase 3 |
| Stripe client | — Phase 9 |

### API host

| Component | Status |
| --- | --- |
| Composition root | ● dead `Neon` connection string removed |
| Authentication + fallback authorization policy | ● |
| `CurrentUserMiddleware` wired after authorization | ● |
| CORS from `Cors:AllowedOrigins` | ● allow-any-origin removed |
| `ExceptionHandlerMiddleware` | ● 401/403/402 mapped; catch-all added; detail withheld |
| `GET /api/session` | ● |
| OpenAPI + Scalar (anonymous, non-Production) | ● |
| `AuditClientController` | ◐ 3 endpoints on stub handlers |
| 7 module controllers | — empty, Phase 2/4 |

### Modules

All eight Application projects now reference `Ledgance.Shared.Application`, FluentValidation and
their own Domain project. Both AI Infrastructure projects reference their Application, Domain and
`Ledgance.Shared.Infrastructure`. `Class1.cs` placeholders were removed from every Domain and
Infrastructure project.

Business content is still Phase 2+: `Ledgance.Audit.Client.Application` holds one command and
three queries against stub data; every other module holds only its `MediatorAnchor`.

### Database

`supabase/migrations/0001_foundation.sql` — `organizations`, `organization_members`,
`organization_subscriptions`, the `is_organization_member` security-definer helper, and
read-only RLS policies for authenticated users. **Not yet applied to a project** (no credentials).

### Tests

| Project | Tests |
| --- | --- |
| `_Tests/Ledgance.TestInfrastructure` | shared harness: `MediatorTestHarness`, `FakeCurrentUserAccessor`, `FakeSubscriptionReader`, `FakeEntitlementService`, `TestIdentity` |
| `_Tests/Ledgance.Shared.Unit.Tests` | 38 — permission registry, entitlement set and catalogue, entitlement resolution and overrides, the four behaviors and their ordering, tenant scoping |
| `_Tests/Ledgance.Audit.Unit.Tests` | 3 — the client slice through the real pipeline |
| `_Tests/Ledgance.Accounting.Unit.Tests` | 0 — wired, populated in Phase 4 |

Convention: slice tests dispatch through `MediatorTestHarness` so authorization, entitlements and
validation are exercised exactly as in production. No test requires real credentials.

---

## Frontend

| Area | Status |
| --- | --- |
| Next.js App Router, TS strict, Tailwind, shadcn/ui | ● |
| `lib/supabase.ts` browser client (anon key) | ● |
| `components/auth-context.tsx` — Supabase Auth | ● sign in / sign up / sign out / password reset / access token |
| `util/http.ts` | ● bearer token attached; API `errors` surfaced |
| `components/query-provider.tsx` | ● `QueryClientProvider` mounted |
| `hooks/session.ts` — `useSession()` | ● not yet consumed by a page |
| `components/theme-context.tsx` | ● now backed by `next-themes`, which also fixes the unprovided `useTheme` in `ui/sonner.tsx` |
| `.env.example` / `.env.local` | ● placeholders only |
| Marketing site, `/login`, `/signup`, `/dashboard` shell | ● UI complete |
| Dashboard data | ◐ still `lib/mock-data.ts` |
| Dashboard sub-routes | — nav links to `/dashboard/{clients,engagements,documents,working-papers,trial-balance,team}` still 404 |
| Accounting product UI | — |

---

## Known issues carried into Phase 2

1. **Supabase has never been contacted.** Every Supabase path — auth, membership, subscriptions,
   repository — compiles and is unit-tested against fakes, but has not run against a real
   project. The migration must be applied and a smoke test run before Phase 2 relies on it.
2. **No organisation provisioning.** Nothing creates an `organizations` row or the first
   `organization_members` row, so a freshly signed-up user authenticates and is then rejected
   with "not a member of any organization". Sign-up onboarding is Phase 2 work.
3. **Frontend product pages are mock-driven** and the dashboard nav links 404.
4. **`Ledgance.Accounting.Unit.Tests` has no tests** — wired but empty until Phase 4.
5. **`QueryableExtensions.PaginateAsync` is LINQ-to-objects** and needs a Supabase-aware
   counterpart (`Range`/`Count`) before paged endpoints hit the database.
6. **`NU1903`** — transitive `Microsoft.OpenApi` 2.0.0 carries a high-severity advisory; it
   resolves when `Microsoft.AspNetCore.OpenApi` ships a patched dependency.
7. **`npm audit`** reports advisories in the existing frontend dependency tree; not triaged.
8. **No CI.** `.github/workflows/` is still empty.

---

## Changes made in Phase 1

**Added** — identity, authorization, entitlement and exception primitives in
`Shared.Application`; Supabase settings, client, tenant-scoped repository, persistence models,
membership reader, current-user middleware, subscription reader, entitlement service, JWT
authentication and the four pipeline behaviors in `Shared.Infrastructure`; `GET /api/session`;
`supabase/migrations/0001_foundation.sql`; `Ledgance.TestInfrastructure` and
`Ledgance.Shared.Unit.Tests`; frontend Supabase client, query provider, session hook and env
files.

**Changed** — module project references wired; `ExceptionHandlerMiddleware` extended;
composition root rebuilt around authentication, scoped CORS and default-deny; mediator
registration, executor ordering and dispatch caching fixed; the two Audit client handlers
simplified onto `ValidationBehavior`; frontend auth and theme contexts rewritten onto Supabase
Auth and `next-themes`; `util/http.ts` error handling and auth header.

**Removed** — `backend/Class1.cs` (dead EF Core test base; its Arrange/Act/Assert intent is
carried by `MediatorTestHarness`); ten `Class1.cs` placeholders; the redundant
`frontend/.eslintrc.json`; the dead `Neon` connection-string lookup; `AllowAnyOrigin` CORS.
