# Architecture Decisions

Newest first. Each entry: decision, why, consequence.

---

## ADR-029 — AI usage is reserved before the work, and refunded only when nothing ran

**Decision.** Every AI operation declares a cost in AI credits, and the allowance is decremented
**before** the provider is called, atomically, by a database function that checks and updates
under one row lock. An operation that produces no result — no provider returned anything —
releases its credits and its ledger row. Once a provider has returned, the credits stay spent
even if the application fails afterwards. An agent run is charged once at the start rather than
per turn.

**Why.** The previous model recorded one unit after each successful completion. That is racy:
two simultaneous requests both read the same remaining balance, both decide there is room, and
both proceed — with an allowance of one, two get through. Checking and decrementing has to be a
single locked operation, and that forces the charge to happen before the work rather than after.
Charging up front also fixes the worse agent failure: a long run that exhausts the allowance
half way through and returns nothing.

**Consequence.** A compensating release exists, but it is one call on one well-defined failure
path, not a refund system: the ledger deletes the row so it always sums to the counter. A
release that itself fails is logged and the units stay spent — the safe direction, and it never
masks the original error. The policy a customer sees is simple: an operation that produces no
result costs nothing; an operation that returns costs its full price.

---

## ADR-028 — An AI-generated report is a persisted draft under professional review

**Decision.** Anything above a single report section is stored as `GeneratedAuditReport` in its
own table, with structured sections, the records the model cited, and the provider and model
that produced it. It enters as `Draft` and leaves that state only through an explicit review by
an engagement **Manager or Partner**. Accepting a draft records who took professional
responsibility for working from it; it never writes `audit_reports` and never finalizes
anything. Organization Admin/Owner oversight grants read access to the engagement, not review
authority. Regenerating a section stores a new draft rather than mutating the reviewed one.

**Why.** An audit report carries professional responsibility that cannot be delegated to a
model. A proposal returned as a blob of text has no state, so nothing can record that a
qualified person examined it — which is exactly what makes AI-assisted reporting defensible.
Persisting the draft also gives the sections somewhere to keep their sources, which is what lets
a reviewer check a claim instead of trusting it.

**Consequence.** The Audit AI module needed its first Infrastructure project and a table
(migration `0011`). Two review states exist in the system that must not be confused: accepting
an AI draft (Manager/Partner, this ADR) and finalizing the audit report
(`FinalizeAuditReportCommand` — Partner only, no open findings, unchanged). The former is input
to the latter and never a substitute for it.

---

## ADR-027 — AI plan gating uses three independent ordered entitlements

**Decision.** Alongside `ai_max_tier`, two further ordered entitlements gate AI:
`ai_report_scope` (`none` → `sections` → `full_draft` → `engagement` → `portfolio` →
`agentic` → `custom`) and `ai_analysis_scope` (`document` → `engagement` → `workflow` →
`portfolio`). A capability declares what it needs on each; a plan includes it only when it
grants all three. `AiEntitlementGate` applies the check for both single completions and agent
loops. A granted value outside its ladder ranks below every level.

**Why.** The product asks for seven Audit plans differentiated by *what AI can do*, not by how
many messages it will send. Four reasoning tiers cannot express that: Micro and Micro-Growth
both want `advanced` reasoning but differ on whether a whole report may be written, and Small
and Medium both want `reasoning` but differ on whether AI may look across engagements. Widening
the tier ladder instead would have coupled report completeness to model cost, which is not the
same axis.

**Consequence.** Plan differentiation is declarative — a capability names its levels and the
catalogue names each plan's grants, so moving a capability between plans is a data change. The
catalogue can resolve the cheapest plan including any capability, which is what lets the UI say
which plan unlocks a locked feature without holding plan rules. A monotonicity test asserts no
ladder ever regresses as a plan gets more expensive, because a single mistyped value would
otherwise sell a downgrade.

---

## ADR-026 — Organization context may travel in the access token, at a bounded staleness cost

**Decision.** `CurrentUserMiddleware` prefers `org_id`/`org_role` claims on the verified access
token and falls back to the `organization_members` lookup when they are absent. Migration
`0009` supplies `custom_access_token_hook`, which Supabase Auth can be configured to run when
minting a token; enabling it is an operational choice, and the application is correct either
way. The hook uses the same selection rule as the fallback reader, so the two can never
disagree about which membership wins.

**Why.** Every Supabase query is a network round trip (~100–300ms measured), and the membership
lookup ran on *every* authenticated request — the single largest fixed cost in the API. Claims
remove it entirely.

**Consequence.** With the hook enabled, a role change or membership removal takes effect at the
next token refresh (≤1 hour) rather than immediately. This is a **deliberate, bounded** trade:
the organization *id* still gates every query through `SupabaseRepository` and row-level
security, so a stale claim can widen what a user may *do* within their own organization for up
to an hour, never *which* organization's data they can reach. Phase 10 should decide whether
that window is acceptable for role demotions, or whether sensitive permissions need a
freshness check.

---

## ADR-025 — Evidence versions are retained, never overwritten

**Decision.** Superseding a document pushes the outgoing version — storage path, size, content
type, note, uploader, timestamp — into the aggregate's version history rather than replacing
the row's file pointer (migration `0010`). Every version stays independently downloadable via
`download-url?version=N`. Re-uploading a file name that already exists on an engagement
versions that document instead of creating a lookalike beside it. Evidence also carries a
category and normalized tags.

**Why.** An auditor must be able to show what a working paper referenced *at the time*, not
only the latest file. The previous implementation claimed versioning — it incremented a version
number — while destroying the evidence of every earlier version, which is worse than not
claiming it.

**Consequence.** Evidence rows carry their history as jsonb, so the history is fetched with the
document and needs no extra query. Rows created before this change start their history at their
current version; earlier supersede counts cannot be reconstructed because they were never
recorded. Accounting documents deliberately do **not** version — they are source attachments,
not audit evidence.

---

## ADR-024 — Activity summaries are active-voice predicates

**Decision.** A handler records what someone did as a predicate beginning with a lowercase verb
— `"approved the audit plan for FY2026 Financial Statement Audit."` — never a standalone
sentence. Every reader composes the sentence by prefixing the actor: `activitySentence()`
renders "You approved the audit plan for …" or "Sarah Whitman approved …".

**Why.** The trail previously stored passive sentences ("The audit plan was approved."), which
the feed prefixed with an actor, producing "You The audit plan was approved." Fixing it in the
UI would have meant parsing English; fixing it at the source makes the record read correctly
everywhere, including raw rows and any future export.

**Consequence.** All 71 call sites were rewritten, and two workflow tests assert the predicate
form so a new handler cannot quietly reintroduce a sentence. The trail is append-only, so rows
written before 2026-08-11 keep the old phrasing and read oddly after an actor name; they are
left as recorded rather than rewritten.

---

## ADR-023 — Billing is a port; the provider's price is the plan; webhooks carry their own scope

**Decision.** Phase 9 puts Stripe behind `IBillingGateway`, `IBillingPriceCatalog`,
`IBillingWebhookVerifier`, `ISubscriptionStore` and `IProcessedEventStore` in
`Shared.Application/Billing`; `Stripe` types appear only in `Shared.Infrastructure/Billing`.
Plan-to-price mapping is configuration (`Stripe:Prices:<PlanCode>`), so a plan with no
configured price is simply not purchasable — the catalog reports it, the UI does not offer it,
and the handler refuses it. Free is never bought and Enterprise never reaches checkout: both are
refused with a reason. **Which plan an organization is on is read from the price the provider
bills**, falling back to the checkout metadata (`organization_id`, `module`, `plan_code`) that
travels onto the subscription, then to the stored plan. Webhook handling verifies the signature
before reading anything, records the provider event id in `billing_events` for idempotency, and
discards any event older than the row's `last_event_at`. `SupabaseSubscriptionStore`
deliberately bypasses `SupabaseRepository`: a webhook has no user and therefore no organization
context, so tenancy comes from an organization id the application resolved itself — signed
metadata, or the stored subscription/customer identifier — and never from request input.

**Why.** ADR-007 already made webhooks the source of truth; this decides *how* an event is
trusted and matched. Signature-then-metadata means a forged payload cannot point at another
organization, and the event id plus timestamp make retries and out-of-order delivery
non-events. Treating the billed price as the authority keeps a plan change made in Stripe's own
portal in sync without a second code path. Keeping prices in configuration means repricing or
opening a new plan is a settings change, and a half-configured environment degrades to "not
purchasable" instead of selling at a wrong price.

**Consequence.** Adding a plan to sale = create the price in Stripe, add one configuration
entry. Replacing the provider = one Infrastructure folder. The webhook path is the only code
allowed to write subscription rows without a user context, and it is the only place that
justifies bypassing the tenant-scoped repository. Entitlements need no billing awareness: they
still resolve from `organization_subscriptions`, which is what billing writes.

---

## ADR-022 — Agent tools are whitelisted mediator requests run as the calling user

**Decision.** The agentic layer (Phase 7) gives an AI agent exactly one way to touch the
application: a per-capability whitelist of `AgentTool`s, each of which dispatches a
read-only mediator request through the full pipeline — authorization, entitlements,
validation, team/entity guards — with the calling user's identity. The engagement or entity
scope is fixed server-side when the tool set is built; the model chooses *which* whitelisted
tool to call and with what arguments, never what the tool is allowed to do. A denial inside
a tool (403/402/409) is serialized into the transcript as that tool's result, so the agent
must work around it — there is no privileged path and no retry-as-someone-else.
`AgentRunnerService` (Shared.Infrastructure) owns the loop: the `agentic` entitlement tier
is required, every provider turn costs one usage unit re-checked against `ai_monthly_units`
before it is taken, tool steps are capped with a forced no-tools final turn, and tool
results are truncated. OpenClaw is the native driver over a `v1/agent/turns` protocol that
only ever returns "call this tool" or "final answer" — tool execution never leaves the
application; when OpenClaw is unavailable the same loop runs over the ordinary chat
providers through `ChatAgentAdapter`'s strict-JSON protocol, falling down the tier chain,
never up. Tools are read-only and the run's answer is a proposal with a full step
transcript; material changes still require a human to use the normal commands.

**Why.** The product context is categorical: agents must never manipulate the database or
bypass application authorization. Reusing the mediator pipeline as the tool boundary means
the agent's authority is *identical* to its caller's, enforced by the same three layers as
every other request — nothing new to audit, and the existing behaviors keep working as the
agent surface grows. Per-turn metering makes a runaway loop an entitlement stop rather than
a bill. Keeping tool execution in-process keeps organization data out of the agentic
provider except for what the workload deliberately shows it.

**Consequence.** New agent capabilities are added by widening a tool whitelist, not by
granting access. Write-capable tools, if ever added, get the same treatment (the pipeline
enforces the command's own permission and domain rules) plus explicit human acceptance.
The transcript (`AgentRunReport.Steps`) is the Phase 8 UI's raw material and the auditor's
evidence of what the agent read.

---

## ADR-021 — Cross-context integration lives in a dedicated Integration assembly

**Decision.** Accounting→Audit context sharing is implemented in
`backend/Integration/Ledgance.Integration.AccountingContext` — the only assembly allowed to
reference both contexts, referenced only by the host. Audit owns a second port,
`ILinkedAccountingSource`, expressed entirely in Audit vocabulary alongside the file-based
`IAccountingContextSource` baseline; Accounting publishes `IAccountingReadContract`
(entity/period/trial-balance snapshots computed from posted ledger lines — no aggregates,
no drafts, no writes). The integration assembly's adapter implements Audit's port against
Accounting's contract and re-verifies, on every call, that (a) the
`accounting_context_sharing` entitlement is present on **both** products and (b) an
Admin/Owner has enabled the per-organization link (`integration_accounting_links`,
migration 0005; managed via `integration:accounting_link:manage`, Admin+). Entitlement
failures surface as 402, a disabled link as 409; the availability query reports the reason
without leaking data. Imports stamp `TrialBalanceSource.LedganceAccounting` and record the
source entity, period and as-of date in the audit trail.

**Why.** The reference rules forbid `Audit.* ↔ Accounting.*` in any layer, yet an in-process
adapter must see both. A neutral assembly at the composition-root level keeps both contexts
ignorant of each other, keeps the future split mechanical (module-boundaries §6: only this
adapter becomes an HTTP client), and gives the link flag and its permissions a home that
belongs to neither product. Requiring the entitlement on both products enforces "subscribed
to both" without plan-name checks.

**Consequence.** `Ledgance.Integration.*` is a new reference-rule category (registered in
`module-boundaries.md` §2); no module may reference it. New shared context (GL drill-down,
statements, documents) is added by widening the published contract and the Audit port —
never by a direct cross-context reference. A `Ledgance.Integration.Unit.Tests` project
covers the adapter and link slices.

---

## ADR-020 — The activity trail scopes by a product-neutral `context_id`

**Decision.** `ActivityEntry`/`RecordedActivity` carry `ContextId` (column `context_id`),
renamed from `EngagementId`/`engagement_id` in migration 0004. Each product records its own
unit of work there: Audit the engagement id, Accounting the accounting-entity id. Reader
queries filter by it.

**Why.** The shared activity trail predates Accounting and had Audit vocabulary baked into a
Shared contract, which violates the rule that Shared never knows a module's concepts. Phase 4
needed entity-scoped accounting history; reusing a field named "engagement" for accounting
entities would have been a standing source of confusion.

**Consequence.** Audit call sites were unaffected (they pass the id positionally); the only
code changes were in Shared. Any future product module scopes its activity the same way
without further schema changes.

---

## ADR-019 — One Ledger module; journal→ledger posting; corrections by reversal only

**Decision.** All books-scoped Accounting features — entities, chart of accounts, fiscal
periods, journal entries, general ledger, trial balance, reports, reconciliation, documents,
activity — live in a single `Modules/Accounting/Ledger` project trio, per the ADR-016
rationale. The bookkeeping core works like a real ledger: a journal entry is drafted
(editable, deletable), must balance (≥2 one-sided lines, debits = credits > 0, memo
required), must be dated inside an existing **open** fiscal period, and on posting
materializes **append-only ledger lines** into their own table. Posted entries are
immutable; the only correction is a reversing entry with swapped lines, linked both ways and
itself posted through the same period rules. The general ledger, trial balance and financial
statements are derived queries over ledger lines — no stored report state. Period close is
blocked while drafts are dated inside the period; parents in the account hierarchy are
summary accounts and reject postings; an account's type is frozen once it has postings or
sub-accounts. Authorization is organization-role based (`accounting:ledger:read` Viewer+ /
`contribute` Member+ / `manage` Manager+ — the last covering entities, chart changes,
period close/reopen and reversals); there is no per-record team confinement, unlike Audit
(ADR-017), because bookkeeping access in small organizations attaches to the org role, not
to an assignment.

**Why.** These capabilities share one aggregate cluster and one guard; separate project
trios would add ceremony without a boundary that matters. The journal→ledger split gives
immutability of the posted record (the property auditors and regulators actually care
about), makes every report a pure function of ledger lines, and lets ledger reads filter
server-side by account and date. Reversal-only correction keeps the trail honest.

**Consequence.** Posting writes the entry and its lines in two steps (no transaction — a
known MVP risk); entry numbers are sequential per entity behind a unique index; a
closing-entry workflow can be added later without changing the model, and until then the
balance sheet presents life-to-date P&L as a current-earnings line. Phase 5 (Accounting AI)
reads through the same repositories and guard; Phase 6 exposes read-only projections to
Audit through Audit's own port, never these repositories.

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

*Note:* `backend/Class1.cs`, a leftover EF Core test-base template that belonged to no project,
has since been deleted. Nothing in the tree references EF Core.

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
