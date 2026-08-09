# Module Boundaries

## 1. Contexts

| Context | Root | Owns |
| --- | --- | --- |
| Audit | `backend/Modules/Audit` | Clients, engagements, planning, materiality, risk, procedures, working papers, evidence, findings, review, reports, audit trail |
| Accounting | `backend/Modules/Accounting` | Entities, fiscal periods, chart of accounts, transactions, journal entries, GL, reconciliation, trial balance, financial reports, documents, history |
| Shared | `backend/Shared` | Mediator, `Result<T>`, paging, cross-cutting primitives |
| Host | `backend/Ledgance.Api` | HTTP surface, middleware, composition root |

Audit and Accounting each have their own `Client`, `Organization`, and `User` slices.
**This duplication is deliberate.** An Audit client and an Accounting client are different
concepts with different lifecycles. Do not "de-duplicate" them into Shared.

## 2. Reference rules

Allowed:

```
<Context>.<Feature>.Domain           → Shared.Application (shared kernel: exceptions/primitives only — ADR-014)
<Context>.<Feature>.Application      → Shared.Application
                                     → <Context>.<Feature>.Domain
<Context>.<Feature>.Infrastructure   → <Context>.<Feature>.Application
                                     → <Context>.<Feature>.Domain
                                     → Shared.Infrastructure
                                     → sibling <Feature>.Application in the SAME context,
                                       to implement a port that sibling publishes
Ledgance.Api                         → any <Context>.<Feature>.Application
                                     → any <Context>.<Feature>.Infrastructure
                                     → Shared.Infrastructure
```

Forbidden:

- `Audit.*` → `Accounting.*` (any direction, any layer)
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
storage abstraction, AI provider abstraction, Stripe client bootstrap, entitlement catalogue.

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

Audit consumes accounting data **only** as a read-only projection behind an explicit contract.

```
Audit.<Feature>.Application
   depends on →  IAccountingContextSource        (port, owned by Audit)
                     ├── ExternalFileContextSource       (CSV / Excel / TB / GL upload)
                     └── LedganceAccountingContextSource (integration adapter)
                                 │
                                 └── calls an Accounting-published read contract
```

Rules:

1. The port is **owned by Audit** and expressed in Audit's own vocabulary. Audit does not
   speak Accounting's language.
2. Accounting publishes a read-only contract (queries returning DTOs). Audit never obtains
   an Accounting aggregate, never mutates Accounting state.
3. Sharing is **opt-in per organisation** and enforced server-side on every call:
   the organisation must be subscribed to both products and must have authorised the link.
4. Every consumption is recorded in the Audit trail — auditors must be able to show where a
   figure came from.
5. `ExternalFileContextSource` is the baseline. Any Audit feature that works only when
   Ledgance Accounting is present is a design error.

## 5. Tenancy

Every tenant-scoped table carries an organisation id. Every query filters on the caller's
organisation, resolved server-side from the authenticated Supabase session — never from a
request body, query string, or header supplied by the client.

Organisation isolation is enforced at three layers:

1. `AuthorizationBehavior` — fail-fast: no resolved organisation context, no handler.
2. `SupabaseRepository<TModel>` — the working guarantee: every read, write and delete of an
   `IOrganizationOwned` model is filtered, stamped, or rejected against the caller's organisation.
3. Supabase row-level security (`supabase/migrations/0001_foundation.sql`) — the backstop for
   any direct client access that does not pass through the API.

A module that bypasses `SupabaseRepository` and queries `Supabase.Client` directly has opted out
of layer 2 and must justify it and filter explicitly.

## 6. Splitting the monolith later

The split is a packaging change, not a redesign, provided the above holds. When it happens:

1. `Ledgance.Api` becomes `Ledgance.Audit.Api` and `Ledgance.Accounting.Api`, each referencing
   only its own modules.
2. `Shared` becomes a published internal NuGet package consumed by both.
3. The Accounting read contract becomes an HTTP/message contract instead of an in-process call —
   only `LedganceAccountingContextSource` changes.

Anything that would make step 3 require touching Audit domain or application code is a
boundary violation, regardless of whether it compiles today.
