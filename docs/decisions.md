# Architecture Decisions

Newest first. Each entry: decision, why, consequence.

---

## ADR-018 — One AI orchestrator; capability catalogs own tier gating; downward-only fallback

**Decision.** All AI traffic flows through `IAiCompletionService` (Shared). The orchestrator
enforces, in order: authorization → `ai_enabled` → tier gate → monthly units → context-size
truncation and gate → tier-routed execution → usage recording. Each product module declares a
**capability catalog** (e.g. `AuditAiCapabilities`) mapping capability → required tier; nothing
else encodes AI gating. Provider routing is tier → provider/model with configuration override
(`Ai:Routing`); defaults are Ollama/basic, OpenAI-`gpt-4o`/advanced,
Anthropic-`claude-opus-5`/reasoning+agentic. On provider failure the orchestrator falls back
**down** the tier chain only; a workload above the plan's tier is a 402, and total provider
failure is a 503 (`AiUnavailableException`). Usage is one unit per completion, per
organization/module/month, recorded only on success. AI output is always an `AiProposalResult`
— AI has no write path into the audit record; humans apply proposals through the normal
commands. The Anthropic adapter uses the official `Anthropic` C# SDK; Ollama and OpenAI are
thin HTTP adapters.

**Why.** One choke point makes authorization, entitlements, cost control and provider swaps
auditable in one place; catalogs keep tier logic out of handlers; downward-only fallback
guarantees a plan's ceiling is never exceeded by an outage.

**Consequence.** Adding a capability = one catalog entry + one slice. Adding a provider = one
adapter + a routing entry. Phase 5 (Accounting AI) reuses everything with
`ProductModule.Accounting`; Phase 7 adds an OpenClaw adapter behind the `agentic` tier.

---

## ADR-017 — Engagement content is confined to the assigned team

**Decision.** Engagement-scoped records (planning, materiality, risks, procedures, working
papers, evidence, findings, review notes, reports, trial balance, activity) are accessible only
to users assigned to that engagement's team, plus organization Admins/Owners for oversight.
Organization permissions (`audit:engagements:*`) gate the operation type; team membership gates
the specific engagement; engagement-role rules (sign-off = team Partner, working-paper approval
= team Manager/Partner, preparer ≠ reviewer/approver) gate professional responsibilities.
Even an org Owner cannot sign off an engagement without being its assigned Partner.

**Why.** Audit confidentiality works this way in real firms: staff see their engagements, not
the whole portfolio, and professional sign-off authority attaches to the engagement role, not
administrative rank.

**Consequence.** `IEngagementAccessGuard.EnsureMemberAsync` is called at the top of every
engagement-scoped handler. Creating an engagement auto-assigns the creator as Partner so an
engagement never exists without someone able to sign it off; the last Partner cannot be removed.

---

## ADR-016 — One Engagement module hosts all engagement-scoped features

**Decision.** Planning, materiality, risks, procedures, working papers, evidence, findings,
review, reports and trial-balance import live as folders (vertical slices) inside a single
`Modules/Audit/Engagement` project trio, not as separate project trios per capability. Closely
related requests share one file per feature folder.

**Why.** These capabilities share one aggregate root, one access guard and one lifecycle; ten
project trios would add csproj ceremony without any boundary that matters. The split that must
stay cheap is Audit vs Accounting, not engagement-feature vs engagement-feature.

**Consequence.** Cross-feature reads within Audit go through feature-owned ports
(`IClientLookup` in Engagement; `IClientEngagementCounter` implemented by Engagement for
Client), keeping even intra-context coupling explicit.

---

## ADR-015 — Organization onboarding lives in Shared; principal vs organization context

**Decision.** `ProvisionOrganizationCommand` (Shared.Application) + its handler
(Shared.Infrastructure) create the organization and the Owner membership at first sign-in.
The middleware no longer rejects authenticated users without membership; it records an
`AuthenticatedPrincipal`, and `AuthorizationBehavior` requires full organization context by
default — `[AllowWithoutOrganization]` (onboarding only) relaxes just the membership
requirement. Organizations are platform-scoped: one organization spans Audit and Accounting.

**Why.** Sign-up must complete before any organization exists, and organizations are not an
Audit or Accounting concept, so neither product module can own provisioning without the other
depending on it.

**Consequence.** `GET /api/session` returns `needsOnboarding: true` for a member-less user;
`POST /api/onboarding/organization` is the only meaningful call in that state. Membership rows
now carry `display_name`/`email` so team pickers don't need auth.users access.

---

## ADR-014 — Domain projects may reference Shared.Application as a shared kernel

**Decision.** `*.Domain` projects reference `Ledgance.Shared.Application` — for
`DomainRuleException` and similar primitives only. Domain still references no infrastructure,
no other module, and nothing else. `DomainRuleException` maps to HTTP 409, distinguishing
"the state does not allow this" from validation (400), permission (403) and entitlement (402)
failures.

**Why.** Domain invariants need a typed failure the API can translate; duplicating the
exception per Domain project is worse than a narrow shared-kernel dependency.

**Consequence.** The former "`*.Domain` → nothing" rule is amended in `module-boundaries.md`.
Any use of Shared.Application types beyond exceptions/primitives in a Domain project is still a
violation.

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

## ADR-010 — The repository is the persistent context; conversations are not

**Decision.** A new session must be able to continue with no access to prior conversation, by
reading, in order: `CLAUDE.md` (permanent rules) → `docs/project-context.md` (what the product
is) → `docs/project-state.md` (where the implementation is) →
`docs/implementation-status.md` (phase tracker) → `decisions.md`, `architecture.md`,
`module-boundaries.md`, `subscription-entitlements.md`, `ai-architecture.md`,
`product-requirements.md` as needed.

Two separations are deliberate: **context vs state** (what the product *should be* vs where the
code *currently is*), and **intent vs truth** (documentation records intent; **the code is the
source of truth**). Where they disagree, inspect the code and correct the documentation.

`ai-architecture.md` additionally marks every section *implemented* or *planned*, because
describing unbuilt AI as if it existed is the failure mode most likely to mislead a later
session.

**Why.** The build spans many phases and many context windows. Anything that lives only in a
conversation is lost.

**Consequence.** A phase updates `project-state.md` and `implementation-status.md` on
completion, and adds an ADR here only for durable decisions — not for a running work log. No
development journal is kept.

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
