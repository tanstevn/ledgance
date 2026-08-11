# CLAUDE.md — Ledgance

Permanent working rules for this repository. Read this first, then
`docs/project-context.md` (what the product is) and `docs/project-state.md`
(where the implementation currently is).

**The code is the source of truth.** If documentation and code disagree, inspect the code, then
correct the documentation.

---

## What this is

Ledgance is a SaaS ecosystem of **two separate professional platforms**:

- **Ledgance Audit** — audit lifecycle management for audit teams, organizations and firms.
- **Ledgance Accounting** — accounting operations and financial information.

They are **separate bounded contexts** that currently share one repository and one deployed API
as a **modular monolith**, for cost effectiveness. The architecture must keep a future split into
separate applications/repositories possible. Do not split them now.

---

## Stack

| Layer | Technology |
| --- | --- |
| Backend | C#, .NET 10, ASP.NET Core Web API |
| Data | Supabase PostgreSQL via the official `Supabase` C# package |
| Auth | Supabase Auth (JWT bearer validated server-side) |
| Payments | Stripe via `Stripe.net`, behind `IBillingGateway` (Phase 9) |
| AI | Ollama, OpenAI, Anthropic, OpenClaw, behind `IAiCompletionService` |
| Frontend | Next.js App Router, React, TypeScript, Tailwind, shadcn/ui |
| Tests | xUnit |

---

## Never do these

1. **No Entity Framework Core.** Use the Supabase C# client's builder/query approach.
2. **No MediatR, and no second mediator.** Use the custom Mediator in `Ledgance.Shared`.
3. **No Audit ↔ Accounting coupling.** Neither context references the other's assemblies.
4. **No client-trusted authorization.** The frontend renders from permissions; the server decides.
5. **No cross-organization data access.** Every tenant query is filtered server-side.
6. **No real secrets in source or docs.** Placeholders only; the user supplies real values.
7. **No AI path that bypasses authorization, entitlements, or domain rules.**
8. **No premature distribution** of the modular monolith.
9. **No plan-name checks in feature code.** Ask the entitlement service.

---

## Architecture

Domain-Driven Design + Vertical Slice Architecture inside a modular monolith.

```
backend/
  Ledgance.Api/                     single host; thin controllers; composition root
  Modules/<Context>/<Feature>/
      *.Application                 commands, queries, handlers, validators, ports
      *.Domain                      entities, value objects, invariants — depends on nothing
      *.Infrastructure              Supabase + external adapters — only when genuinely needed
  Shared/
      Ledgance.Shared.Application   mediator abstractions, Result<T>, identity, entitlements
      Ledgance.Shared.Infrastructure mediator impl, behaviors, Supabase, auth
  _Tests/
supabase/migrations/                schema + row-level security
docs/
frontend/
```

Allowed references: Domain → Shared.Application only (shared kernel: exceptions/primitives —
ADR-014). Application → Shared.Application + own Domain. Infrastructure → own
Application/Domain + Shared.Infrastructure (+ a sibling feature's Application to implement its
port). Api → any Application/Infrastructure, never a Domain. Shared never references a module.
Details in `docs/module-boundaries.md`.

Organize by business capability, not by `Controllers/Services/DTOs/Repositories`. Keep business
logic in the feature that owns it. Create an Infrastructure project only when a feature needs one.

---

## The custom Mediator

Lives in `Ledgance.Shared`. Inspect it before writing a slice.

- `IRequest<TResponse>`, `ICommand<TResponse>`, `IQuery<TResponse>`
- `IRequestHandler<TRequest,TResponse>.HandleAsync(request, ct)`
- `IPipelineBehavior<TRequest,TResponse>` — must be **open generic**, ordered by `[PipelineOrder]`
  (**lower runs further out**; unattributed runs innermost)
- `IMediator.SendAsync(request, ct)`

Registered pipeline, outermost first:

| Order | Behavior |
| --- | --- |
| 0 | `LoggingBehavior` — names and durations only, never payloads |
| 100 | `AuthorizationBehavior` — default-deny; `[AllowAnonymousRequest]`, `[RequiresPermission]` |
| 200 | `EntitlementBehavior` — `[RequiresEntitlement]` capability checks |
| 300 | `ValidationBehavior` — FluentValidation |

### Slice conventions

- Command + result + validator + handler are colocated: one file per request, or one file per
  closely-related request family within a feature folder.
- Handlers return `Result<T>` / `PaginatedResult<T>`. Expected failures are results, not
  exceptions; domain invariant violations throw `DomainRuleException` (→ HTTP 409).
- **Handlers never call validators** — `ValidationBehavior` does.
- Engagement-scoped handlers call `IEngagementAccessGuard.EnsureMemberAsync` first (ADR-017).
- Activity summaries are **active-voice predicates** starting lowercase — `"approved the audit
  plan for {engagement.Name}."`, never `"The audit plan was approved."` — because every reader
  renders "You " / "<Name> " + summary as one sentence (ADR-024).
- Reads that do not depend on each other run under one `Task.WhenAll`. Every Supabase query is a
  network round trip, so sequential awaits are the main source of page latency.
- Every module Application assembly needs a `MediatorAnchor` and an entry in
  `Ledgance.Api/DependencyInjection.cs`; module permissions register in the
  `AddLedganceSharedInfrastructure` callback.

---

## Data access

- Supabase types (`Supabase.Client`, `BaseModel`) exist **only** in Infrastructure.
- Application declares ports; Infrastructure implements them.
- Persistence models are separate from domain entities; map at the boundary.
- Use `SupabaseRepository<TModel>` (`Shared.Infrastructure`). A model implementing
  `IOrganizationOwned` is automatically filtered on read, stamped on insert, and rejected on
  cross-organization update. `Query()` returns the native Supabase builder, already scoped.
- Bypassing the repository opts out of tenant safety and requires explicit justification and
  manual filtering.

---

## Identity, authorization, tenancy

```
Supabase access token → JwtBearer validation → CurrentUserMiddleware
→ CurrentUser { UserId, Email, OrganizationId, Role, Permissions } → ICurrentUserAccessor
```

- Organization membership comes from `organization_members`, **never** from a client-supplied
  value. A caller cannot choose the organization they act in.
- Roles: `Viewer < Member < Manager < Admin < Owner`.
- Permissions are namespaced strings (`organization:members:manage`, `audit:engagement:approve`).
  Modules register grants through `PermissionRegistry`; no role logic in feature code.
- Isolation has three layers: `AuthorizationBehavior` (fail-fast) → `SupabaseRepository`
  (working guarantee) → row-level security (backstop).
- Every endpoint requires authentication by default via a fallback authorization policy.

Error mapping: 400 validation/argument · 401 `UnauthenticatedException` · 403
`ForbiddenException` · **402 `EntitlementException`** (upgrade required) · 410 cancelled ·
500 otherwise, with detail withheld.

---

## Subscriptions and entitlements

Plans: Audit — Free, Professional (≤30 users), Organization (≤75), Firm (≤150), Enterprise.
Accounting — Free, Solo ($14.99/mo), Team, Professional, Enterprise. Enterprise is Contact Sales.

- `SubscriptionPlanCatalog` is the **only** place plan values are declared.
- Resolution: catalogue defaults → `Subscriptions:Plans:*` configuration → per-organization
  `entitlement_overrides`. A non-Active/Trialing subscription resolves to Free.
- Capabilities gate via `[RequiresEntitlement]`. Numeric limits are checked in handlers with
  `EntitlementSet.RequireWithinLimit`. `-1` is unlimited; an unknown key reads `0` (fails closed).
- **Free plans must be genuinely useful.** Upgrade pressure comes from scale, depth and AI, never
  from blocking a core workflow midway.

## Billing

Stripe lives behind `IBillingGateway`/`IBillingWebhookVerifier`/`IBillingPriceReader`
(Shared.Application/Billing); Stripe types appear only in `Shared.Infrastructure/Billing`.

- **Webhooks are the source of truth** (ADR-007/ADR-023): the redirect back from checkout never
  grants access. Signature verified before the payload is read, event ids recorded for
  idempotency, older events ignored by timestamp.
- Plan ↔ price mapping is configuration (`Stripe:Prices:<PlanCode>`, must be a `price_…` id).
  A plan with no usable price is not purchasable, server-side. **Never hardcode an amount in
  the frontend** — displayed prices are read back from Stripe.
- Free and Enterprise never reach checkout: nothing to buy, and Enterprise is Contact Sales.

---

## AI

Domain and Application never name a provider. Features depend on AI abstractions; an
orchestration layer picks the provider from **authorization → entitlement tier → task capability
→ complexity → context size → cost**. Never default to the most expensive model.

Ollama = cost-effective baseline · OpenAI = advanced general work · Anthropic = complex reasoning
and large context · OpenClaw = agentic orchestration.

AI reads only what the requesting user could already read. AI output is a **proposal**: a human
accepts anything material, and the acceptance is recorded. Agents act through whitelisted
application capabilities, never directly against the database.

See `docs/ai-architecture.md` for what is implemented versus planned.

---

## Frontend

- Reuse and extend `components/ui` (shadcn/ui). Do not add a second UI kit.
- Style with the semantic tokens in `app/globals.css` (`background`, `card`, `primary`,
  `success`, `warning`, `chart-1..5`, `--radius`), not raw colour literals.
- Call the API through `hooks/query.ts` + `util/http.ts`, which mirror the backend
  `Result<T>` / `PaginatedResult<T>` envelope and attach the Supabase bearer token.
- Every data surface handles **loading, empty, error, populated**. Route-level `loading.tsx`
  gives navigation an instant response; without it the router holds the previous page.
- Reuse the shared pieces in `components/workspace.tsx` (`FileDropZone`, `Pagination`,
  `RecordAvatar`, `StatusPill`, `ProgressTrack`, `EmptyCard`, `ErrorCard`) rather than
  re-implementing them per page.
- `components/ui/**` is vendored upstream code and is lint-exempt; author new components in
  `components/`.
- Only `NEXT_PUBLIC_*` values reach the browser. The service-role key must never appear here.

---

## Configuration and secrets

- Backend: `appsettings.json` → `appsettings.{Environment}.json` →
  `appsettings.local.json` (git-ignored) → environment variables.
- Frontend: `.env.example` is committed with placeholders; `.env.local` is git-ignored.
- Committed files carry placeholders only. Never ask the user for real credentials.

---

## Testing

xUnit. Test projects: `Ledgance.Shared.Unit.Tests`, `Ledgance.Audit.Unit.Tests`,
`Ledgance.Accounting.Unit.Tests`, plus the shared `Ledgance.TestInfrastructure`.

- Slice tests dispatch through `MediatorTestHarness`, which runs the **real** mediator and
  behaviors, so authorization, entitlements and validation are exercised as in production.
- Use the provided fakes for third-party services. No test may require real credentials.
- Cover domain rules, handlers, validation, authorization, organization isolation, entitlements,
  AI routing and authorization, and integration boundaries. Do not test trivial code.

---

## Code style

Follow the existing conventions: block-scoped namespaces, 4-space indent, brace on the same line,
`_camelCase` private fields, constructor injection.

**Comments:** prefer self-explanatory code. Do not write comments that restate the code
(`// get the client`, `// return the result`) or large blocks describing an obvious
implementation. Comment only what the code cannot express: architectural reasoning, non-obvious
business rules, security considerations, external API limitations, workarounds.

---

## Working agreement

1. Inspect existing code before changing it; follow its conventions; reuse what works.
2. Do not rewrite working functionality without cause, and do not add abstractions for
   architectural appearance.
3. Each phase: implement → build backend → build frontend → run tests → fix → rebuild → rerun →
   remove throwaway comments → update `docs/project-state.md` and `docs/implementation-status.md`.
4. Never leave a phase with known compilation or test failures.
