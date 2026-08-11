# Module Boundaries

## 1. Contexts

| Context | Root | Owns |
| --- | --- | --- |
| Audit | `backend/Modules/Audit` | Clients, engagements, planning, materiality, risk, procedures, working papers, evidence, findings, review, reports, audit trail |
| Accounting | `backend/Modules/Accounting` | Entities, fiscal periods, chart of accounts, transactions, journal entries, GL, reconciliation, trial balance, financial reports, documents, history |
| Shared | `backend/Shared` | Mediator, `Result<T>`, paging, identity and permissions, entitlements, activity trail, AI and agent abstractions, Supabase bootstrap |
| Integration | `backend/Integration` | Cross-context adapters; belongs to neither context (ADR-021) |
| Host | `backend/Ledgance.Api` | HTTP surface, middleware, composition root |

Audit and Accounting each have their own `Client`, `Organization`, and `User` slices.
**This duplication is deliberate.** An Audit client and an Accounting client are different
concepts with different lifecycles. Do not "de-duplicate" them into Shared.

Most of those slices are still **scaffolds**: `Accounting/Client`, `Accounting/Organization`,
`Accounting/User` and `Audit/Organization` contain only a `MediatorAnchor`, and their `*.Domain`
projects are empty. `Audit/User` carries one real query (`GetOrganizationMembersQuery`, behind
`GET /api/audit/users`, used for team pickers). The projects exist so the boundary is already in
place when those capabilities are built.

## 2. Reference rules

Allowed:

```
<Context>.<Feature>.Domain           → Shared.Application (shared kernel: exceptions/primitives only — ADR-014)
<Context>.<Feature>.Application      → Shared.Application
                                     → <Context>.<Feature>.Domain
                                     → sibling <Feature>.Application in the SAME context,
                                       to consume the contract that sibling publishes
                                       (e.g. Audit.AI.Application → Audit.Engagement.Application;
                                        Accounting.AI.Application → Accounting.Ledger.Application)
<Context>.<Feature>.Infrastructure   → <Context>.<Feature>.Application
                                     → <Context>.<Feature>.Domain
                                     → Shared.Infrastructure
                                     → sibling <Feature>.Application in the SAME context,
                                       to implement a port that sibling publishes
Ledgance.Api                         → any <Context>.<Feature>.Application
                                     → any <Context>.<Feature>.Infrastructure
                                     → Ledgance.Integration.*
                                     → Shared.Infrastructure
Ledgance.Integration.*               → Audit Application layers (to implement Audit-owned ports)
                                     → Accounting Application layers (to call published read contracts)
                                     → Shared.Infrastructure
```

The `backend/Integration` assemblies are the **only** place both contexts may be referenced
together. They belong to neither context, are referenced only by the host (and their own
tests), and neither context references them.

Forbidden:

- `Audit.*` → `Accounting.*` (any direction, any layer)
- Any module → `Ledgance.Integration.*`
- Any `*.Domain` → anything beyond the shared kernel
- `Shared.*` → any module
- `Ledgance.Api` → any `*.Domain`
- Cross-**feature** references inside the same context, except through that feature's
  published Application contract (its ports and requests)

## 3. What belongs in Shared

Shared is for **mechanism**, not **meaning**.

Belongs: mediator abstractions and implementation, `Result<T>`/`PaginatedResult<T>`,
pagination/sorting helpers, pipeline behaviors (logging, validation, authorization,
entitlement enforcement), tenant/user context abstraction, Supabase client bootstrap,
storage abstraction, AI provider abstraction, entitlement catalogue, and the billing ports with
their Stripe adapter (`IBillingGateway` and friends — billing is organization-level, belonging
to neither product, and both products' subscriptions resolve through it).

Modules extend Shared through explicit seams rather than by adding module concepts to it:

| Seam | How a module uses it |
| --- | --- |
| `PermissionRegistry` | Contribute module permission strings via the `modulePermissions` callback on `AddLedganceSharedInfrastructure`. |
| `SupabaseRepository<TModel>` | Compose it inside module Infrastructure to implement module-owned ports. |
| `[RequiresPermission]` / `[RequiresEntitlement]` | Declare requirements on module requests; Shared enforces them. |
| `MediatorAnchor` | Register the module's Application assembly for handler and validator discovery. |

Does not belong: anything named after an audit or accounting concept — `Engagement`,
`WorkingPaper`, `JournalEntry`, `TrialBalance`, `Client`. If a type would only make sense to
one of the two products, it lives in that product.

## 4. Cross-context integration (Accounting → Audit)

Audit consumes accounting data **only** as a read-only projection behind explicit,
Audit-owned contracts (implemented in Phase 6):

```
Audit.Engagement.Application
   ├── IAccountingContextSource   (file payloads — CsvAccountingContextSource, the baseline)
   └── ILinkedAccountingSource    (the organization's own Ledgance Accounting books)
             └── LinkedAccountingSourceAdapter        (backend/Integration/
                        │                              Ledgance.Integration.AccountingContext)
                        ├── IAccountingLinkStore       (per-organization opt-in flag)
                        └── IAccountingReadContract    (published by Accounting.Ledger.Application:
                                                        entity/period/trial-balance snapshots
                                                        from posted ledger lines only)
```

Rules:

1. The port is **owned by Audit** and expressed in Audit's own vocabulary. Audit does not
   speak Accounting's language.
2. Accounting publishes a read-only contract (queries returning DTOs). Audit never obtains
   an Accounting aggregate, never mutates Accounting state.
3. Sharing is **opt-in per organisation** and enforced server-side on every call: the
   `accounting_context_sharing` entitlement must be present on **both** products and an
   Admin/Owner must have enabled the link (`integration_accounting_links`, migration 0005).
   The adapter re-verifies all three on every read.
4. Every consumption is recorded in the Audit trail — auditors must be able to show where a
   figure came from (the import activity names the accounting entity, period and as-of date).
5. The external-file source is the baseline. Any Audit feature that works only when
   Ledgance Accounting is present is a design error.

## 5. Tenancy

Every tenant-scoped table carries an organisation id. Every query filters on the caller's
organisation, resolved server-side from the authenticated Supabase session — never from a
request body, query string, or header supplied by the client.

Organisation isolation is enforced at three layers:

1. `AuthorizationBehavior` — fail-fast: no resolved organisation context, no handler.
2. `SupabaseRepository<TModel>` — the working guarantee: every read, write and delete of an
   `IOrganizationOwned` model is filtered, stamped, or rejected against the caller's organisation.
3. Supabase row-level security — the backstop for any direct client access that does not pass
   through the API. Each migration enables RLS and org-scoped read policies for the tables it
   creates (`0001` foundation, `0002` audit, `0003` AI usage, `0004` accounting, `0005` the
   integration link); `0006` grants the Data API roles explicitly — `service_role` full access,
   `authenticated` read-only, `anon` nothing — because newer Supabase projects no longer grant
   them on `public` by default.

A module that bypasses `SupabaseRepository` and queries `Supabase.Client` directly has opted out
of layer 2 and must justify it and filter explicitly.

## 6. Splitting the monolith later

The split is a packaging change, not a redesign, provided the above holds. When it happens:

1. `Ledgance.Api` becomes `Ledgance.Audit.Api` and `Ledgance.Accounting.Api`, each referencing
   only its own modules.
2. `Shared` becomes a published internal NuGet package consumed by both.
3. The Accounting read contract becomes an HTTP/message contract instead of an in-process call —
   only `LinkedAccountingSourceAdapter` (in `backend/Integration`) changes.

Anything that would make step 3 require touching Audit domain or application code is a
boundary violation, regardless of whether it compiles today.
