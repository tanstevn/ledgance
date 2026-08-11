# Ledgance — Architecture

This document states how the system is designed and the rules that govern it. It is **not** a
statement of what is built — see `project-state.md` for that. The rules here are binding
whether or not a given area has been implemented yet.

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
│   ├── Integration/             # the only assemblies that may see both contexts (ADR-021)
│   ├── Shared/
│   │   ├── Ledgance.Shared.Application      # Mediator abstractions, Result, paging
│   │   └── Ledgance.Shared.Infrastructure   # Mediator implementation + DI
│   └── _Tests/
├── frontend/                     # Next.js App Router
├── supabase/migrations/          # schema + row-level security
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
Ledgance.<Context>.<Feature>.Application      → Shared.Application, own Domain,
                                                sibling <Feature>.Application in the same context
Ledgance.<Context>.<Feature>.Domain           → Shared.Application (shared kernel only — ADR-014)
Ledgance.<Context>.<Feature>.Infrastructure   → own Application, own Domain, Shared.Infrastructure,
                                                sibling <Feature>.Application in the same context
```

- **Domain** — entities, value objects, invariants, domain services. No Supabase, no HTTP, no DI
  container types. It references `Shared.Application` **only** for shared-kernel primitives such as
  `DomainRuleException` (ADR-014); any other use of a Shared type in a Domain project is a violation.
  The `*.Domain` projects for the placeholder module slices are empty by design.
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
| `[PipelineOrder(short)]` | Expected on every pipeline behavior. **Lower** order runs further out; an unattributed behavior sorts as `short.MaxValue` and runs innermost. |

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
| 100 | `AuthorizationBehavior` | Default-deny. Requires an authenticated caller with an organization unless the request is `[AllowAnonymousRequest]` (no principal needed) or `[AllowWithoutOrganization]` (principal but no membership — onboarding only); enforces `[RequiresPermission]`. |
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
| `DomainRuleException` | 409 — the state does not allow the operation (ADR-014) |
| `EntitlementException` | 402 — "upgrade required", distinguishable from 403 by the client |
| `AiUnavailableException` | 503 — every eligible AI provider failed |
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
             → CurrentUserMiddleware → AuthenticatedPrincipal { UserId, Email }
                                     → organisation membership lookup
             → CurrentUser { UserId, Email, OrganizationId, Role, Permissions }
             → ICurrentUserAccessor (scoped, synchronous, resolved once per request)
```

- Organisation membership comes from `organization_members`, never from a client-supplied
  value, so a caller cannot choose the organisation they operate in. A custom `org_id`/`org_role`
  access-token claim is used as a fast path when a Supabase Auth hook supplies it.
- An authenticated user with **no** membership keeps only the `AuthenticatedPrincipal`; the
  middleware does not reject them. `AuthorizationBehavior` requires full organisation context by
  default, so onboarding — marked `[AllowWithoutOrganization]` — is the only thing such a user can
  do (ADR-015).
- `OrganizationRole` is `Viewer < Member < Manager < Admin < Owner`.
- Permissions are strings (`"organization:members:manage"`, later `"audit:engagement:approve"`).
  `PermissionRegistry` is a startup-populated grant table; modules contribute their own
  permissions through the `modulePermissions` callback on `AddLedganceSharedInfrastructure`.
  No role-to-permission logic is duplicated in feature code.
- The token is validated with the project's symmetric `JwtSecret` when one is configured
  (HS256 projects). An **empty** `JwtSecret` selects the asymmetric path: signing keys are read
  directly from the JWKS URL derived from `Supabase:Url`
  (`{Url}/auth/v1/.well-known/jwks.json`) and cached for the process lifetime — Supabase Auth publishes
  no OIDC discovery document, so `MetadataAddress` cannot be used. Rotating the project's signing
  keys requires an API restart. Validation failures log the reason only, never token contents.

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
- Enum values bind and render by name — `JsonStringEnumConverter` is registered globally, so
  request bodies and response DTOs agree on `"FinancialStatement"` rather than an ordinal.
- `GET /api/session` returns the server-resolved identity, organisation id and name, role,
  permissions, per-module plan, the organisation's activated `Products`, and `needsOnboarding`
  for a member-less caller. Clients render from it; they never authorize with it.

## 8a. Billing

Stripe is reached only through ports declared in `Shared.Application/Billing` —
`IBillingGateway` (customers, checkout, portal, plan change, cancellation),
`IBillingWebhookVerifier`, `IBillingPriceReader`, `IBillingPriceCatalog`, `ISubscriptionStore`
and `IProcessedEventStore`. Stripe types exist only in `Shared.Infrastructure/Billing`, so the
billing slices are testable against a fake provider and the provider is replaceable.

```
Checkout:  POST api/billing/checkout → ensure customer → persist it → session → redirect
Truth:     POST api/billing/webhook  → verify signature → dedupe by event id
                                     → ignore older events → upsert subscription
Entitlement: subscription row → EntitlementService → every gated operation
```

- **Webhooks are the source of truth** (ADR-007). The success redirect displays state; it never
  grants it. The webhook endpoint is the only anonymous product endpoint, authenticated by
  payload signature and returning 400 when it does not verify.
- Checkout metadata carries organization, module and plan onto the subscription, so later
  events resolve their own scope without trusting the caller.
- The plan a subscription grants is read from the **price** it bills, so a change made in
  Stripe's own portal lands in the application too (ADR-023).
- `ISubscriptionStore` is the one place that bypasses `SupabaseRepository`: the webhook path has
  no user and therefore no organization context, so it filters explicitly on ids the
  application resolved itself.

## 9. Frontend

Next.js App Router + React + TypeScript + Tailwind + shadcn/ui.

- `app/` — routes. Marketing site at `/`, `/accounting`, `/audit`, `/pricing`; auth at `/login`
  and `/signup`; onboarding and subscription at `/onboarding`, `/subscribe`; product under
  `/dashboard` (per-platform sections, per-engagement and per-entity workspaces, AI pages, billing).
- `components/ui/` — the shadcn/ui primitive set. **Reuse and extend these; do not introduce a second UI kit.**
- `components/` — composed app components (`dashboard-layout`, `workspace`, `marketing-header`,
  `pricing-plans`, `cross-sell`, `ai/`, `auth/`, contexts).
- `lib/audit-types.ts`, `lib/accounting-types.ts` — frontend view models per product;
  `lib/plans.ts` — plan presentation derived from the API's catalogue; `lib/utils.ts` — `cn()`.
- `lib/supabase.ts` — the browser Supabase client (publishable/anon key only).
- `components/auth-context.tsx` — Supabase Auth session: sign in, sign up, sign out,
  password reset, OAuth sign-in (`signInWithOAuth`), and the current access token.
- `types/`, `util/http.ts`, `hooks/query.ts` — the typed API layer mirroring the backend
  `Result<T>` / `PaginatedResult<T>` envelope, over TanStack Query. `util/http.ts` attaches the
  Supabase access token as a bearer header and surfaces the API's `errors` array on failure.
- `hooks/session.ts` — `useSession()` over `GET /api/session`; `hooks/use-toast.ts` — notifications.
- Providers are composed in `app/layout.tsx`: `ThemeProvider` (next-themes) → `QueryProvider`
  → `AuthProvider`.
- Design tokens are HSL CSS variables in `app/globals.css`, consumed through
  `tailwind.config.ts` (`background`, `card`, `primary`, `success`, `warning`, `chart-1..5`, `--radius`).
  Use the semantic token names, not raw colour literals.

## 10. Cross-context integration

Audit must work with **or without** Ledgance Accounting.

- Audit never references an Accounting Domain or Application assembly.
- Shared accounting context (trial balance, GL, balances, statements, periods) reaches Audit
  through an explicit integration contract owned by a dedicated integration assembly —
  a read-only, permissioned projection, never direct entity access.
- Audit models an *accounting context source* abstraction with at least two implementations:
  external import (CSV/Excel/trial balance/GL) and Ledgance Accounting.

Realised in `backend/Integration/Ledgance.Integration.AccountingContext`
(`LinkedAccountingSourceAdapter`), the only assembly referencing both contexts and referenced
only by the host — ADR-021. Today the shared projection covers entities, periods and the trial
balance; widening it is a change to the published contract and the Audit-owned port, never a
cross-context reference.

See `module-boundaries.md` for the enforceable rules.

## 11. Non-negotiables

1. No EF Core. No MediatR. No second mediator.
2. No direct Audit ↔ Accounting domain coupling.
3. Authorization is server-side; the frontend is never the enforcement point.
4. Organisation isolation applies to every read and write of tenant data.
5. Secrets come from configuration/environment only, never source, never the client bundle.
6. AI cannot bypass authorization, entitlements, or domain rules.
