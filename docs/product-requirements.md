# Ledgance — Product Requirements

`project-context.md` is authoritative for product objectives, platform boundaries, AI strategy
and subscription structure. This document holds the MVP capability scope in more detail.

## 1. Positioning

Ledgance is a SaaS ecosystem of two professional products sharing an account, an
organisation model, and a design language — but sold, used, and reasoned about separately.

| | Ledgance Audit | Ledgance Accounting |
| --- | --- | --- |
| Buyer | Audit firms and professional audit teams | Businesses and accounting teams |
| Unit of work | Engagement | Fiscal period |
| MVP priority | **First** | Second |
| Individual/freelance plan | **No** — team and firm only | Yes (Solo) |

## 2. Shared foundations

- **Accounts & auth** — Supabase Auth: registration, login, logout, password recovery, session management.
- **Organisations** — every user belongs to an organisation; all data is organisation-isolated.
- **Roles** — organisation-level roles drive permissions; enforced server-side.
- **Subscriptions** — Stripe-backed, per product, with centralised entitlements.
- **Activity trail** — both products record who did what, when, to which record.

## 3. Ledgance Audit — MVP scope

| Capability | Requirement |
| --- | --- |
| Clients | Create and manage audit clients with contact, industry, and profile data. |
| Engagements | Per-client engagements with type, period, fiscal year end, status lifecycle, budget vs actual. |
| Engagement teams | Assign members with engagement roles (partner, manager, senior, staff); access to an engagement is by assignment. |
| Audit planning | Scope, objectives, timeline, planned approach per engagement. |
| Materiality | Overall materiality, performance materiality, clearly-trivial threshold, with basis and rationale recorded. |
| Risk assessment | Identify and rate risks, link to assertions and to planned responses. |
| Audit procedures | Programme of procedures per area, linked to risks, assignable, with completion state. |
| Working papers | Structured papers with preparer/reviewer sign-off states and cross-references. |
| Evidence | Document upload, versioning, linkage to papers and procedures. |
| Findings | Raise, classify, track, and resolve findings; link to evidence. |
| Review | Review notes, reviewer sign-off, and a clear review status per paper and per engagement. |
| Audit reports | Draft and produce engagement output from working papers and findings. |
| Audit trail | Immutable activity history across the engagement. |

### Accounting context for Audit

Audit must be fully usable by a firm that does **not** use Ledgance Accounting. It therefore
accepts external accounting context:

- CSV and Excel import
- Trial balance
- General ledger
- Financial statements
- Supporting documents
- Client-provided data from other accounting systems

When the organisation also runs Ledgance Accounting and has authorised the link, the same
context can be sourced directly (see `module-boundaries.md` §4).

## 4. Ledgance Accounting — MVP scope

| Capability | Requirement |
| --- | --- |
| Accounting entities | One organisation may keep books for multiple entities. |
| Fiscal periods | Period definition, open/close state; postings respect period state. |
| Chart of accounts | Hierarchical accounts with type and classification. |
| Transactions | Record and categorise business transactions. |
| Journal entries | Balanced double-entry postings with lines, references, and attachments. |
| General ledger | Per-account ledger derived from postings. |
| Reconciliation | Reconcile accounts against statements; track differences. |
| Trial balance | Period trial balance with drill-down to ledger. |
| Financial reports | Core statements for a period. |
| Documents | Attach and manage source documents. |
| Activity history | Full change history over accounting records. |

## 5. Subscription plans

See `subscription-entitlements.md` for the enforced limits.

**Accounting:** Free · Solo ($14.99/mo) · Team · Professional · Enterprise (Contact Sales)
**Audit:** Free · Professional (≤30 users) · Organization (≤75 users) · Firm (≤150 users) · Enterprise (Contact Sales)

Free must be a genuinely usable product, not a demo. Pressure to upgrade comes from user
count, volume, storage, collaboration depth, AI allowance and model tier, automation, and
advanced analysis — not from crippling core workflows.

## 6. AI

AI is a first-class part of both products, not a bolt-on. Scope and provider routing are in
`ai-architecture.md`. Product-level requirements:

- AI sees only context the requesting user is already authorised to see.
- AI output is **assistive**. It drafts, summarises, and suggests. It never silently commits a
  material audit or accounting decision — a human accepts every material change, and
  acceptance is recorded in the activity trail.
- AI availability, usage allowance, model tier, and context size are entitlement-driven.

## 7. Payments

Stripe only — no custom payment processing. Customers, products, prices, checkout,
subscription lifecycle (upgrade/downgrade/cancel), payment status, webhooks, and entitlement
synchronisation. International Stripe-supported methods, plus Philippine local methods
(GCash, Maya) where Stripe supports them.

## 8. Experience requirements

Modern, premium, professional, minimalistic, friendly, intuitive, responsive, accessible.
Minimalistic means restrained, not plain.

Every data surface must handle four states explicitly: **loading**, **empty**, **error**, **populated**.
Tables, forms, dashboards, charts, cards, status indicators, and animation must read as one
product across Audit and Accounting.
