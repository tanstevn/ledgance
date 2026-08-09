# Ledgance — Project State

**Where the implementation currently is.** For what the product should be, read
`project-context.md`. This document is updated at the end of every phase.

**Last verified:** 2026-08-10, against the repository (not from memory).

---

## Position

| | |
| --- | --- |
| Last completed phase | **Phase 1 — Foundation & Shared Infrastructure** |
| Current phase | none in progress |
| Next phase | **Phase 2 — Audit Core MVP** (not started) |

No Audit or Accounting business functionality has been implemented.

---

## Build and test status

Verified by running the commands, not assumed.

| Check | Result |
| --- | --- |
| `dotnet build backend/Ledgance.slnx` | succeeded — 0 errors, 0 C# warnings |
| `dotnet test backend/Ledgance.slnx` | **41 passed, 0 failed** (38 shared, 3 audit, 0 accounting) |
| `npx tsc --noEmit` (frontend) | clean |
| `npm run build` (frontend) | compiled successfully, lint clean |
| API smoke test | host boots; `/api/session` and `/api/audit-client/all` → 401 unauthenticated; invalid token → 401; `/` → 302; `/openapi/v1.json` → 200; unknown route → 404 |

Outstanding build warnings: `NU1903` on transitive `Microsoft.OpenApi` 2.0.0 (high-severity
advisory, resolves when `Microsoft.AspNetCore.OpenApi` ships a patched dependency).

---

## What is implemented

### Shared application layer — `backend/Shared/Ledgance.Shared.Application`

Custom Mediator abstractions (`IRequest`, `ICommand`, `IQuery`, `IRequestHandler`,
`IPipelineBehavior`, `IMediator`, `IExecutor`, `[PipelineOrder]`); `Result<T>` /
`PaginatedResult<T>`; `QueryableExtensions`; `CurrentUser` and `ICurrentUserAccessor`;
`OrganizationRole`; `PermissionRegistry` and `SharedPermissions`; `[AllowAnonymousRequest]` and
`[RequiresPermission]`; `UnauthenticatedException` / `ForbiddenException` /
`EntitlementException`; `ProductModule`, `PlanCode`, `Entitlements`, `AiTiers`, `EntitlementSet`,
`IEntitlementService`, `ISubscriptionReader`, `SubscriptionPlanCatalog`,
`[RequiresEntitlement]`.

### Shared infrastructure — `backend/Shared/Ledgance.Shared.Infrastructure`

Mediator implementation (executor cache; duplicate `IMediator` registration and the
`[PipelineOrder]` null-reference both fixed); the four pipeline behaviors; Supabase settings and
service-role client registration with best-effort initialization; `SupabaseRepository<TModel>`;
`IEntityModel` / `IOrganizationOwned` / `TenantScope`; organization, member and subscription
persistence models; `CurrentUserContext` and `CurrentUserMiddleware`;
`IOrganizationMembershipReader`; `SupabaseSubscriptionReader`; `EntitlementService`;
`AddSupabaseAuthentication` (symmetric JWT secret or JWKS); `AddLedganceSharedInfrastructure`.

### API host — `backend/Ledgance.Api`

Composition root wired to shared infrastructure and Supabase authentication; fallback
authorization policy requiring an authenticated user on every endpoint; `CurrentUserMiddleware`
after authorization; CORS restricted to `Cors:AllowedOrigins`; `ExceptionHandlerMiddleware`
mapping 400/401/403/402/410/500 with detail withheld on unexpected errors;
`GET /api/session`; OpenAPI and Scalar exposed anonymously outside Production.

### Database — `supabase/migrations/0001_foundation.sql`

`organizations`, `organization_members`, `organization_subscriptions`, the
`is_organization_member` security-definer helper, and read-only row-level security policies for
authenticated users. **Written but never applied** — no Supabase project has been contacted.

### Modules

All eight Application projects reference `Ledgance.Shared.Application`, FluentValidation and
their own Domain project. Both AI Infrastructure projects reference their Application, Domain and
`Ledgance.Shared.Infrastructure`.

Content is scaffolding only: `Ledgance.Audit.Client.Application` holds one command and three
queries returning **hard-coded sample data**; every other module holds only a `MediatorAnchor`.
All Domain projects are empty.

### Tests

`Ledgance.TestInfrastructure` provides `MediatorTestHarness` (dispatches through the real
mediator and real behaviors), `FakeCurrentUserAccessor`, `FakeSubscriptionReader`,
`FakeEntitlementService` and `TestIdentity`. `Ledgance.Shared.Unit.Tests` has 38 tests over the
permission registry, entitlement set and catalogue, entitlement resolution and override
precedence, the four behaviors and their ordering, and tenant scoping.
`Ledgance.Audit.Unit.Tests` has 3 tests exercising the client slice through the real pipeline.
`Ledgance.Accounting.Unit.Tests` is wired but empty.

### Frontend

Supabase browser client (`lib/supabase.ts`); real Supabase Auth in `components/auth-context.tsx`
(sign in, sign up, sign out, password reset, access token); `util/http.ts` attaches the bearer
token and surfaces the API's `errors` array; `QueryClientProvider` mounted via
`components/query-provider.tsx`; `hooks/session.ts` (`useSession`) over `GET /api/session`;
`components/theme-context.tsx` backed by `next-themes`; `.env.example` and `.env.local`.

Marketing site, `/login`, `/signup` and the `/dashboard` shell are visually complete. Dashboard
data still comes from `lib/mock-data.ts`.

---

## What remains

Everything in Phases 2–13. Nearest work:

1. **Organization provisioning** — nothing creates the first `organizations` and
   `organization_members` rows. This is the first blocker for Phase 2.
2. **Apply and smoke-test the Supabase schema** against a real project.
3. **Audit Core MVP** — clients, engagements, teams, planning, materiality, risk, procedures,
   working papers, evidence, findings, review, reporting, activity trail, external accounting
   context import.
4. Audit AI, Accounting Core, Accounting AI, the integration boundary, agentic AI, product UI,
   Stripe, security review, quality, polish, MVP review.

---

## Known issues and limitations

1. **No Supabase path has ever executed against a live project.** Auth, membership,
   subscriptions and the repository compile and are unit-tested against fakes only. Treat all of
   it as unverified against the real service until smoke-tested.
2. **A newly signed-up user cannot use the product.** They authenticate successfully and are then
   rejected with "This account is not a member of any organization", because no onboarding flow
   creates an organization or membership row.
3. **`Ledgance.Audit.Client.Application` returns fabricated data.** Its handlers are stubs; they
   are wired to the pipeline but have no persistence.
4. **Dashboard sub-routes 404.** `components/dashboard-layout.tsx` links to
   `/dashboard/{clients,engagements,documents,working-papers,trial-balance,team}`; none exist.
5. **`Ledgance.Accounting.Unit.Tests` contains no tests.**
6. **`QueryableExtensions.PaginateAsync` is LINQ-to-objects** and needs a Supabase-aware
   counterpart (`Range` / `Count`) before paged endpoints hit the database.
7. **No Stripe and no AI code exists.** Configuration placeholders are present; nothing reads
   them.
8. **`NU1903`** transitive advisory; **`npm audit`** advisories in the frontend tree, untriaged.
9. **No CI.** `.github/workflows/` is empty.
10. **`components/ui/**` is lint-exempt** for four rules (ADR-013), so upstream shadcn files are
    not held to the project ruleset.
11. **`Modules/Accounting/Client/*` has no documented purpose.** The Accounting capability list
    in `project-context.md` §3 contains no "client" concept — clients belong to Audit. The
    module is empty scaffolding; decide in Phase 4 whether to repurpose it (for example as
    accounting entities) or remove it. The same applies to `AccountingClientController`.
12. **The seven empty module controllers use the default `api/[controller]` route**, unlike
    `AuditClientController`, which declares `api/audit-client`. Settle one route convention in
    Phase 2 before endpoints are published.

---

## Important files

| Path | Why it matters |
| --- | --- |
| `CLAUDE.md` | Permanent working rules |
| `docs/project-context.md` | What the product is |
| `backend/Ledgance.Api/DependencyInjection.cs` | Composition root, middleware order, module registration |
| `backend/Shared/Ledgance.Shared.Infrastructure/Mediator/` | The custom Mediator — read before writing a slice |
| `backend/Shared/Ledgance.Shared.Infrastructure/Behaviors/` | Logging, authorization, entitlement, validation |
| `backend/Shared/Ledgance.Shared.Infrastructure/Supabase/SupabaseRepository.cs` | Tenant-safe data access |
| `backend/Shared/Ledgance.Shared.Infrastructure/Identity/CurrentUserMiddleware.cs` | How organization context is resolved |
| `backend/Shared/Ledgance.Shared.Application/Subscriptions/SubscriptionPlanCatalog.cs` | The only place plan values are declared |
| `backend/Modules/Audit/Client/.../CreateClientCommand.cs` | Reference vertical slice |
| `backend/_Tests/Ledgance.TestInfrastructure/MediatorTestHarness.cs` | Testing convention |
| `supabase/migrations/0001_foundation.sql` | Schema and row-level security |
| `frontend/components/ui/` | The shadcn/ui component set to reuse |
| `frontend/hooks/query.ts`, `frontend/util/http.ts` | Typed API layer |

---

## Configuration expected

Placeholders are committed; real values go in `backend/Ledgance.Api/appsettings.local.json` and
`frontend/.env.local`, both git-ignored.

Backend: `Supabase:{Url, AnonKey, ServiceRoleKey, JwtSecret, Audience}` ·
`Cors:AllowedOrigins` · `Subscriptions:Plans` · `Stripe:{SecretKey, PublishableKey,
WebhookSecret}` · `Ai:{Ollama, OpenAI, Anthropic, OpenClaw}`.

Frontend: `NEXT_PUBLIC_API_URL` · `NEXT_PUBLIC_SUPABASE_URL` · `NEXT_PUBLIC_SUPABASE_ANON_KEY`.

Only Supabase keys are consumed today; Stripe and AI entries are placeholders for later phases.
