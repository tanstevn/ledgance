# Subscriptions & Entitlements

`project-context.md` §9–§12 is authoritative for plan structure and intent. This document covers
how entitlements are modelled and enforced in code.

## 1. Principle

Plan knowledge lives in **one place**. Domain code asks "am I allowed to do X / how much of X
is left?", never "is this the Professional plan?".

```
Stripe subscription  →  Plan  →  Entitlement set  →  IEntitlementService  →  callers
```

A plan-name comparison anywhere outside the entitlement catalogue is a defect.

## 2. Plans

### Audit

| Plan | Users | Positioning |
| --- | --- | --- |
| Free | up to 2 | 1 client, 2 engagements, limited AI — a genuinely useful entry experience |
| Professional | up to 30 | Working audit teams |
| Organization | up to 75 | Multi-team firms |
| Firm | up to 150 | Large firms |
| Enterprise | negotiated | Contact Sales |

There is **no individual/freelance Audit plan**.

### Accounting

| Plan | Positioning |
| --- | --- |
| Free | Genuinely usable for one small entity |
| Solo — $14.99/month | Single practitioner |
| Team | Small team, shared books |
| Professional | Multiple entities, deeper reporting and AI |
| Enterprise | Contact Sales |

## 3. Entitlement dimensions

Entitlements are per **organisation** and per **product**. A value is either a numeric limit,
a boolean capability, or a tier.

| Key | Type | Applies to |
| --- | --- | --- |
| `max_users` | limit | both |
| `max_clients` | limit | Audit |
| `max_engagements` | limit | Audit |
| `max_entities` | limit | Accounting |
| `max_transactions_per_period` | limit (per fiscal period) | Accounting |
| `storage_bytes` | limit | both |
| `ai_enabled` | capability | both |
| `ai_monthly_units` | limit | both |
| `ai_max_tier` | tier (`basic` / `advanced` / `reasoning` / `agentic`) | both |
| `ai_max_context_tokens` | limit | both |
| `advanced_analysis` | capability | both |
| `advanced_review` | capability | Audit |
| `automation` | capability | both |
| `integrations` | capability | both |
| `api_access` | capability | both |
| `accounting_context_sharing` | capability | cross-product |
| `enterprise_support` | capability | both |

Every plan declares **every** key. A plan carries `0` for the other product's dimensions — an
Audit plan sets `max_entities`/`max_transactions_per_period` to `0`, an Accounting plan sets
`max_clients`/`max_engagements` to `0` — so a limit check on the wrong product fails closed
rather than reading as unlimited. `-1` is unlimited.

There are **nine plan codes**, not ten: `Free` is a single shared code used for both products,
plus four paid Audit codes and four paid Accounting codes. A Free organisation therefore gets the
same entitlement row for Audit and Accounting.

Enterprise values are negotiated and stored per organisation as an override on top of the
plan's catalogue entry (`organization_subscriptions.entitlement_overrides`, a jsonb map).

## 4. Enforcement

Three enforcement points, all server-side:

1. **Pipeline behavior** — `EntitlementBehavior` reads `[RequiresEntitlement(module, capability)]`
   off the request and checks it before the handler runs. Used for boolean capabilities.
2. **Handler** — checks that need domain state (e.g. "adding this member would exceed
   `max_users`") call `IEntitlementService.GetAsync` and then
   `EntitlementSet.RequireWithinLimit`. Used for numeric limits.
3. **AI router** — `ai_max_tier`, `ai_monthly_units`, and `ai_max_context_tokens` gate provider
   and model selection before any call leaves the process. See `ai-architecture.md`.

Both raise `EntitlementException`, which `ExceptionHandlerMiddleware` renders as the standard
`Result` envelope with **HTTP 402**. 402 rather than 403 so a client can tell "upgrade required"
from "your role may not do this".

`EntitlementSet` API: `Has(capability)`, `Limit(key)`, `Tier(key)`, `IsWithinLimit(key, total)`,
`RequireCapability(key)`, `RequireWithinLimit(key, total)`. `-1` means unlimited; an unknown key
reads as `0`, so a missing entitlement fails closed.

`IEntitlementService` is scoped and memoises per (organisation, module) for the request.

The frontend may read entitlements to hide or annotate UI. That is presentation only. Every
gated operation is re-checked server-side.

## 5. Stripe mapping **[implemented — Phase 9]**

- One Stripe **Customer** per organisation, created on first checkout and reused after.
- One Stripe **Product/Price** per paid plan, created in the Stripe dashboard and mapped to a
  plan code through `Stripe:Prices:<PlanCode>` configuration — the value must be a `price_…`
  identifier; anything else is rejected at startup with a warning. A plan with no usable price is
  reported as not purchasable and is refused server-side, so nothing is ever sold at a guessed
  price. The **amount shown to customers is read back from Stripe** (`IBillingPriceReader`,
  cached), so the pricing page, subscribe page and plan picker cannot drift from the invoice.
- **Checkout Sessions** for purchase (`POST /api/billing/checkout`), the **Billing Portal** for
  payment methods and invoices (`POST /api/billing/portal`), a subscription item swap for
  upgrade/downgrade (`POST /api/billing/change-plan`) and `cancel_at_period_end` for cancellation
  and resumption (`POST /api/billing/cancel`).
- No row at all resolves to Free, as does any row whose status is not Active or Trialing.

- **Webhooks are the source of truth** for subscription state (`POST /api/billing/webhook`,
  anonymous and signature-verified). The application never infers entitlement from a redirect or
  a client-side signal; `/subscribe/success` shows a pending state until the event lands.
- Handling verifies the signature before reading the payload, is idempotent (`billing_events`
  keyed by the provider event id) and tolerates out-of-order delivery (an event older than the
  stored `last_event_at` is discarded).
- Handled events: `checkout.session.completed`,
  `customer.subscription.created|updated|deleted`, `invoice.paid`, `invoice.payment_failed`.
  Anything else is acknowledged and ignored.
- Which plan is active is read from the **price** the provider bills, so a change made in
  Stripe's own portal syncs back; checkout metadata (organisation, module, plan) covers the
  first event, before any price is known to us.
- Payment methods are not listed in code: the account's own configuration decides what a
  customer sees, which is how cards and wallets reach international customers and how local
  methods such as GCash and Maya reach Philippine customers when the account and the plan's
  recurring terms support them. `Stripe:PaymentMethodTypes` can pin the list when needed.

## 6. Free tier intent

Free plans must let a user complete a real workflow end to end — a real engagement in Audit,
a real period in Accounting — with real AI assistance at the basic tier. The upgrade trigger
is scale and depth, never an artificial wall in the middle of core work.

## 7. Configuration

`SubscriptionPlanCatalog` (in `Ledgance.Shared.Application.Subscriptions`) declares the default
entitlement values for all nine plan codes. Resolution order, last wins:

1. `SubscriptionPlanCatalog` defaults
2. `Subscriptions:Plans:<PlanCode>:<entitlement>` in configuration
3. `organization_subscriptions.entitlement_overrides` for that organisation (negotiated
   Enterprise terms)

A non-`Active`/`Trialing` subscription resolves to `Free` regardless of the stored plan.

Stripe keys, price ids, and webhook secrets come from `appsettings.local.json` or environment
variables. Placeholders only in committed files.
