# Architecture Decisions

Newest first. Each entry: decision, why, consequence.

---

## ADR-013 — Lint policy: vendored `components/ui` is exempt, not patched

**Decision.** `eslint.config.mjs` disables `no-explicit-any`, `no-empty-object-type`,
`no-unused-vars` and `react-hooks/set-state-in-effect` for `components/ui/**` only. The legacy
`.eslintrc.json` was removed; flat config is the single source.

**Why.** Those files are unmodified shadcn/ui primitives we re-sync from upstream. Patching them
means re-patching on every update, and `next build` runs lint, so the failures blocked every
build and any future CI.

**Consequence.** Project code is held to the full ruleset. Anything we author in
`components/ui` should move to `components/` instead.

---

## ADR-012 — Validation runs in the pipeline, not in handlers

**Decision.** `ValidationBehavior` (order 300) executes every registered `IValidator<TRequest>`
before the handler. Handlers no longer inject validators or call `ValidateAndThrowAsync`.

**Why.** The previous convention put four lines of identical boilerplate in every handler and
made it possible to forget one. Authorization is ordered ahead of validation so an unauthorized
caller learns nothing about the request shape.

**Consequence.** A slice supplies a validator and nothing else; the two existing Audit client
handlers were simplified accordingly.

---

## ADR-011 — Service-role client with in-code tenancy; RLS as the backstop

**Decision.** The API holds the Supabase service-role key and enforces organisation isolation in
code through `SupabaseRepository<TModel>`, which filters, stamps, and guards every
`IOrganizationOwned` model. Row-level security policies exist and grant authenticated users
read access to their own organisation's rows only; no write policies are granted to end users.

**Why.** Server-side code needs to act across a request in one consistent identity, and several
operations (subscription sync, membership resolution) legitimately run before or outside a user
context. Making the repository the single choke point gives one place to audit, one place to
test, and a mechanical guarantee — while RLS still protects anything reaching Postgres directly
from a browser.

**Consequence.** The service-role key must never leave the server. Bypassing
`SupabaseRepository` bypasses layer 2, so it requires explicit justification and manual
filtering. `TenantScope.Stamp`/`Guard` are unit-tested directly.

---

## ADR-010 — Documentation lives in `/docs` and is updated per phase

**Decision.** Seven documents form the durable project context: `architecture.md`,
`product-requirements.md`, `module-boundaries.md`, `subscription-entitlements.md`,
`ai-architecture.md`, `implementation-status.md`, `decisions.md`.

**Why.** The build spans many phases. Context that only exists in a conversation is lost.

**Consequence.** A phase that changes architecture also updates the relevant document and adds
an ADR here. `implementation-status.md` is updated at the end of every phase.

---

## ADR-009 — Free tiers are functional products

**Decision.** Free plans allow a complete real workflow, including basic AI. Upgrade pressure
comes from scale (users, volume, storage), depth (advanced analysis, review, automation), and
AI tier — never from blocking a core workflow midway.

**Why.** Professional audit and accounting buyers evaluate by doing real work. A crippled demo
does not convert.

**Consequence.** Limits must be chosen so that "one small engagement" and "one small entity"
genuinely fit inside Free.

---

## ADR-008 — Entitlements are centralised; plan names never appear in domain code

**Decision.** A single entitlement catalogue maps plan → entitlement set. Callers ask
`IEntitlementService` about a capability or limit. Enforcement happens in a pipeline behavior,
in handlers that need domain state, and in the AI router.

**Why.** Scattered `if (plan == "Professional")` checks make pricing changes a code-wide edit
and make gaps invisible.

**Consequence.** Adding a plan or changing a limit is a configuration change. A plan-name
comparison outside the catalogue is treated as a defect.

---

## ADR-007 — Stripe webhooks are the source of truth for subscription state

**Decision.** Subscription and entitlement state is updated from verified Stripe webhooks, not
from checkout redirects or client signals. Handlers are signature-verified, idempotent, and
order-tolerant.

**Why.** Redirects are unreliable and forgeable; webhooks are the only authoritative channel.

**Consequence.** The success page shows a pending state until the webhook lands, rather than
optimistically granting access.

---

## ADR-006 — AI is provider-agnostic in domain code, tier-routed at the edge

**Decision.** Domain and Application depend only on AI abstractions. Provider selection happens
in an orchestration layer using authorization, entitlement tier, task capability, complexity,
context size, and cost. Ollama is the baseline; OpenAI for advanced general work; Anthropic for
complex reasoning and large context; OpenClaw for agentic workflows.

**Why.** Provider economics and capabilities change constantly. Coupling features to a vendor
makes both switching and tiering expensive.

**Consequence.** Adding or swapping a provider touches only Infrastructure and routing policy.

---

## ADR-005 — AI is assistive and never bypasses authorization

**Decision.** AI context is assembled through the same authorization path as a normal query;
there is no privileged AI data path. AI output is a proposal requiring human acceptance before
it enters the audit or accounting record, and acceptance is recorded.

**Why.** Audit and accounting records carry professional and legal weight. An AI shortcut around
access control or around professional judgement is unacceptable in this domain.

**Consequence.** Every AI feature needs an explicit context-assembly step and an accept/reject
step in the UI, plus activity-trail entries for both.

---

## ADR-004 — Audit must work without Ledgance Accounting

**Decision.** Audit's accounting context is an abstraction with an external-file implementation
(CSV, Excel, trial balance, GL, statements, client documents) as the baseline, and a Ledgance
Accounting adapter as an optional, per-organisation opt-in. Audit never touches Accounting
domain entities.

**Why.** Audit firms audit clients who use other systems. Requiring Ledgance Accounting would
disqualify most of the market, and direct coupling would prevent the future split.

**Consequence.** Every Audit feature is designed against the abstraction first. Integration
work is confined to one adapter.

---

## ADR-003 — Supabase client instead of Entity Framework Core

**Decision.** Data access uses the official Supabase C# client and its query builder. EF Core is
not used.

**Why.** Supabase is the platform of record for database, auth, and storage. A second data
stack duplicates the model and fights Supabase's row-level security and auth integration.

**Consequence.** Supabase types stay in Infrastructure. Application defines ports; Infrastructure
implements them. Persistence models are mapped to domain entities at the boundary. Row-level
security policies are part of the schema deliverable, not an afterthought.

*Note:* the untracked-by-any-project file `backend/Class1.cs` is a leftover EF Core test-base
template. It compiles into nothing and must not be revived as-is.

---

## ADR-002 — Custom Mediator, not MediatR

**Decision.** The in-house Mediator in `Ledgance.Shared` is the dispatch mechanism.
MediatR is not added, and no competing request/handler abstraction is created.

**Why.** It already exists, is understood, is licence-free, and its conventions are established
in `Ledgance.Audit.Client.Application`.

**Consequence.** Handlers implement `IRequestHandler<TRequest,TResponse>.HandleAsync`. Pipeline
behaviors must be open-generic and must carry `[PipelineOrder]`. Each module Application
assembly needs a `MediatorAnchor` registered in `Ledgance.Api/DependencyInjection.cs`.

---

## ADR-001 — Modular monolith in one repository

**Decision.** Audit and Accounting live in one repository and one deployed API host, as
separate bounded contexts with no cross-context references.

**Why.** MVP cost and speed. Two repositories and two deployments now would multiply
infrastructure and coordination cost with no user-visible benefit.

**Consequence.** Boundaries must be enforced by discipline and reference rules
(`module-boundaries.md`) rather than by process isolation. The split later must be a packaging
change, not a redesign.
