# Ledgance — Architecture

This document states how the system is designed and the rules that govern it. It is **not** a
statement of what is built — see `project-state.md` for that. Sections describing Audit,
Accounting and cross-context integration are binding design rules for work that has not started.

## 1. Shape of the system

Ledgance is a **modular monolith** hosting two conceptually separate products:

- **Ledgance Audit** — for professional audit teams and firms
- **Ledgance Accounting** — for accounting entities and their books

They share one repository and one deployable API host today for cost and speed.
They are **separate bounded contexts** and must stay separable so each can later become
its own repository, deployment, and domain (`api.ledgance-audit.com`,
`api.ledgance-accounting.com`).

```
/
├── backend/
│   ├── Ledgance.slnx
│   ├── Ledgance.Api/            # single ASP.NET Core host, controllers per module
│   ├── Modules/
│   │   ├── Audit/<Feature>/     # Application / Domain / Infrastructure
│   │   └── Accounting/<Feature>/
│   ├── Shared/
│   │   ├── Ledgance.Shared.Application      # Mediator abstractions, Result, paging
│   │   └── Ledgance.Shared.Infrastructure   # Mediator implementation + DI
│   └── _Tests/
├── frontend/                     # Next.js App Router
└── docs/
```

## 2. Architectural styles in force

| Style | How it applies here |
| --- | --- |
| Domain-Driven Design | Each module owns its own domain model. No shared domain types across Audit/Accounting. |
| Vertical Slice | A *feature* is the unit of organisation. Command/Query + Result + Validator + Handler live in one file or one folder — not spread across `Controllers/Services/DTOs/Repositories`. |
| Modular Monolith | One process, many isolated modules. Cross-module calls go through explicit contracts only. |
| Dependency Inversion | Application depends on abstractions; Infrastructure implements them. Domain depends on nothing. |

## 3. Project layering rules

For a module feature `Modules/<Context>/<Feature>/`:

```
Ledgance.<Context>.<Feature>.Application      → Shared.Application, own Domain
Ledgance.<Context>.<Feature>.Domain           → nothing
Ledgance.<Context>.<Feature>.Infrastructure   → own Application, own Domain, Shared.Infrastructure
```

- **Domain** — entities, value objects, invariants, domain services. No Supabase, no HTTP, no DI container types.
- **Application** — commands, queries, handlers, validators, port interfaces (`I...Repository`, `I...Reader`). Depends on `Ledgance.Shared.Application` for the Mediator abstractions and `Result<T>`.
- **Infrastructure** — Supabase clients, external API adapters, port implementations. **Create only when a feature genuinely needs it.**

`Ledgance.Api` references module **Application** assemblies (for the request/result types the
controllers bind to) and module **Infrastructure** assemblies (for DI registration only).
It must never reference a module **Domain** assembly.

## 4. Request flow

```
HTTP  →  Controller (thin)  →  IMediator.SendAsync(command|query)
      →  IPipelineBehavior<,> chain (ordered by [PipelineOrder])
      →  IRequestHandler<TRequest,TResponse>
      →  domain + ports (Supabase adapters in Infrastructure)
      →  Result<T> / PaginatedResult<T>  →  JSON
```

Controllers contain no business logic. They map a route to a request object and return
whatever the handler returns. See `Ledgance.Api/Controllers/Audit/AuditClientController.cs`
for the reference shape.

## 5. Custom Mediator (Ledgance.Shared)

The project has its **own** Mediator. MediatR is not used and must not be added.

`Ledgance.Shared.Application/Abstractions`:

| Type | Purpose |
| --- | --- |
| `IRequest<TResponse>` | Marker for anything dispatchable. |
| `ICommand<TResponse>` / `IQuery<TResponse>` | Intent-revealing markers over `IRequest`. |
| `IRequestHandler<TRequest,TResponse>` | `Task<TResponse> HandleAsync(TRequest, CancellationToken)`. |
| `IPipelineBehavior<TRequest,TResponse>` | Cross-cutting wrapper; `HandleAsync(request, next, ct)`. |
| `IMediator` | `Task<TResponse> SendAsync<TResponse>(IRequest<TResponse>, CancellationToken)`. |
| `IExecutor` | Internal bridge that lets `Mediator` resolve a closed generic handler from an open request. |
| `[PipelineOrder(short)]` | **Required** on every pipeline behavior. Higher order runs first (outermost). |

`Ledgance.Shared.Infrastructure/Mediator`:

- `Mediator` — closes `Executor<TRequest,TResponse>` over the runtime request type and invokes it.
- `Executor<,>` — resolves the handler, resolves behaviors, folds them into a delegate chain.
- `DependencyInjection.AddMediatorFromAssemblies(params Assembly[])` — scans for
  `IRequestHandler<,>` implementations (transient) and **open-generic** `IPipelineBehavior<,>`
  implementations (transient).

### Conventions to follow

- One feature file per request: `XCommand`, `XCommandResult`, `XCommandValidator`, `XCommandHandler`.
- Handlers return `Result<T>` or `PaginatedResult<T>` — not raw DTOs, not exceptions for expected failures.
- Handlers do **not** call validators. `ValidationBehavior` runs every registered
  `IValidator<TRequest>` before the handler.
- Every new module Application assembly needs a `MediatorAnchor` class and must be added to the
  assembly array in `Ledgance.Api/DependencyInjection.cs`.
- Pipeline behaviors must be **open generic** (`class Foo<TRequest,TResponse> : IPipelineBehavior<TRequest,TResponse>`)
  and should carry `[PipelineOrder]`; an unattributed behavior runs innermost.

### Registered pipeline

Lower `[PipelineOrder]` runs further out, so the chain is:

| Order | Behavior | Responsibility |
| --- | --- | --- |
| 0 | `LoggingBehavior` | Request name, organization id, duration, failure. Never payloads. |
| 100 | `AuthorizationBehavior` | Default-deny. Requires an authenticated caller with an organization unless the request is `[AllowAnonymousRequest]`; enforces `[RequiresPermission]`. |
| 200 | `EntitlementBehavior` | Enforces `[RequiresEntitlement]` capability checks. |
| 300 | `ValidationBehavior` | FluentValidation; throws `ValidationException` on failure. |

Authorization deliberately precedes validation so that an unauthorized caller learns nothing
about the shape of the request.

## 6. Results and errors

`Result<T>` (`Successful`, `Data`, `Errors`) and `PaginatedResult<T>` are the universal
transport. `ExceptionHandlerMiddleware` converts thrown exceptions into the same envelope:

| Exception | Status |
| --- | --- |
| `ArgumentNullException` | 400 |
| `FluentValidation.ValidationException` | 400 (per-error messages) |
| `UnauthenticatedException` | 401 |
| `ForbiddenException` | 403 |
| `EntitlementException` | 402 — "upgrade required", distinguishable from 403 by the client |
| `InvalidOperationException` | 500 |
| `OperationCanceledException` | 410 |
| anything else | 500, logged, detail withheld from the response |

Validators are registered with `AddValidatorsFromAssemblies` over the module assemblies and
invoked by `ValidationBehavior`, not by handlers.

## 7. Data access

**Entity Framework Core is not used and must not be introduced.**

Persistence is Supabase PostgreSQL through the official Supabase C# client (`Supabase` 1.6.0),
using its table/query builder. Rules:

- Supabase types live in Infrastructure only. Application and Domain never see
  `Supabase.Client` or `BaseModel`.
- Application defines the port (e.g. `IClientRepository`); Infrastructure implements it.
- Persistence models (Supabase `[Table]`/`[Column]` POCOs) are separate from domain entities;
  map at the Infrastructure boundary.
- Every query against organisation-scoped data must filter by the caller's organisation id.
  This is enforced server-side, never assumed from the client.

### `SupabaseRepository<TModel>`

`Ledgance.Shared.Infrastructure.Supabase.SupabaseRepository<TModel>` is the reusable tenant-safe
entry point. Module Infrastructure composes it to implement its own ports.

- `TModel` must be a `BaseModel` implementing `IEntityModel`.
- If it also implements `IOrganizationOwned`, **every** query from `Query()`, `FindAsync`,
  `ListAsync`, `CountAsync` and `DeleteAsync` is filtered by the caller's organisation;
  `InsertAsync` stamps it and `UpdateAsync` rejects a row from another organisation.
- `Query()` returns the Supabase query builder already scoped, so feature code keeps using the
  native builder (`.Filter`, `.Order`, `.Range`, `.Get`) rather than a hand-rolled query language.

The schema lives in `supabase/migrations/`. Row-level security policies there are the backstop;
`SupabaseRepository` is the working guarantee. See ADR-011.

## 7a. Identity and organisation context

Supabase Auth issues the access token; ASP.NET validates it (`AddSupabaseAuthentication`),
and `CurrentUserMiddleware` turns the verified principal into a `CurrentUser`:

```
Bearer token → JwtBearer validation → ClaimsPrincipal
             → CurrentUserMiddleware → organisation membership lookup
             → CurrentUser { UserId, Email, OrganizationId, Role, Permissions }
             → ICurrentUserAccessor (scoped, synchronous, resolved once per request)
```

- Organisation membership comes from `organization_members`, never from a client-supplied
  value, so a caller cannot choose the organisation they operate in. A custom `org_id`/`org_role`
  access-token claim is used as a fast path when a Supabase Auth hook supplies it.
- An authenticated user with no membership is rejected with `ForbiddenException`.
- `OrganizationRole` is `Viewer < Member < Manager < Admin < Owner`.
- Permissions are strings (`"organization:members:manage"`, later `"audit:engagement:approve"`).
  `PermissionRegistry` is a startup-populated grant table; modules contribute their own
  permissions through the `modulePermissions` callback on `AddLedganceSharedInfrastructure`.
  No role-to-permission logic is duplicated in feature code.
- The token is validated with the project's symmetric `JwtSecret` when configured, otherwise
  against the project's published JWKS document.

## 8. API host

`Ledgance.Api` — controllers, middleware, composition root.

- Configuration order: `appsettings.json` → `appsettings.{Environment}.json` →
  `appsettings.local.json` (optional, git-ignored) → environment variables.
- Middleware order: CORS → `ExceptionHandlerMiddleware` → routing → authentication →
  authorization → `CurrentUserMiddleware` → endpoints.
- A **fallback authorization policy** requires an authenticated user on every endpoint.
  OpenAPI, Scalar, the `/` redirect and the 404 fallback opt out with `AllowAnonymous`.
- CORS origins come from `Cors:AllowedOrigins`; there is no allow-any-origin policy.
- OpenAPI + Scalar UI at `/scalar/v1` outside Production; `/` redirects there.
- `GET /api/session` returns the server-resolved identity, organisation, role, permissions and
  per-module plan. Clients render from it; they never authorize with it.

## 9. Frontend

Next.js App Router + React + TypeScript + Tailwind + shadcn/ui.

- `app/` — routes. Marketing site at `/`, auth at `/login` and `/signup`, product under `/dashboard`.
- `components/ui/` — the shadcn/ui primitive set. **Reuse and extend these; do not introduce a second UI kit.**
- `components/` — composed app components (`dashboard-layout`, `marketing-header`, contexts).
- `lib/types.ts` — frontend view models; `lib/utils.ts` — `cn()`.
- `lib/supabase.ts` — the browser Supabase client (anon key only).
- `components/auth-context.tsx` — Supabase Auth session: sign in, sign up, sign out,
  password reset, and the current access token.
- `types/`, `util/http.ts`, `hooks/query.ts` — the typed API layer mirroring the backend
  `Result<T>` / `PaginatedResult<T>` envelope, over TanStack Query. `util/http.ts` attaches the
  Supabase access token as a bearer header and surfaces the API's `errors` array on failure.
- `hooks/session.ts` — `useSession()` over `GET /api/session`.
- Providers are composed in `app/layout.tsx`: `ThemeProvider` (next-themes) → `QueryProvider`
  → `AuthProvider`.
- Design tokens are HSL CSS variables in `app/globals.css`, consumed through
  `tailwind.config.ts` (`background`, `card`, `primary`, `success`, `warning`, `chart-1..5`, `--radius`).
  Use the semantic token names, not raw colour literals.

## 10. Cross-context integration

Audit must work with **or without** Ledgance Accounting.

- Audit never references an Accounting Domain or Application assembly.
- Shared accounting context (trial balance, GL, balances, statements, periods) reaches Audit
  through an explicit integration contract owned by a dedicated integration slice —
  a read-only, permissioned projection, never direct entity access.
- Audit models an *accounting context source* abstraction with at least two implementations:
  external import (CSV/Excel/trial balance/GL) and Ledgance Accounting.

See `module-boundaries.md` for the enforceable rules.

## 11. Non-negotiables

1. No EF Core. No MediatR. No second mediator.
2. No direct Audit ↔ Accounting domain coupling.
3. Authorization is server-side; the frontend is never the enforcement point.
4. Organisation isolation applies to every read and write of tenant data.
5. Secrets come from configuration/environment only, never source, never the client bundle.
6. AI cannot bypass authorization, entitlements, or domain rules.
