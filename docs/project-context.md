# Ledgance — Project Context

**This document is the authoritative statement of what Ledgance is and should be.**
It describes product intent, not implementation status. For current implementation state see
`project-state.md`; for phase tracking see `implementation-status.md`.

A future session with no access to prior conversation should be able to read this document and
understand the intended product.

---

## 1. Ledgance

Ledgance is a SaaS ecosystem containing **two distinct professional platforms**:

1. **Ledgance Audit**
2. **Ledgance Accounting**

They are conceptually separate products with separate buyers, separate domain models and
separate subscriptions. They are implemented today inside **one repository as a modular
monolith** for cost effectiveness and development speed.

The repository contains `backend/`, `frontend/`, `docs/`, `supabase/`, `.gitignore`,
`.editorconfig` and related project files.

### Future separation

The architecture must preserve the option of splitting the two platforms into separate
applications and repositories, potentially served from distinct domains such as:

- `ledgance.audit.com` / `api.ledgance-audit.com`
- `ledgance.accounting.com` / `api.ledgance-accounting.com`

That split must be a **packaging change, not a redesign**. Do not prematurely distribute the
system. The current priority is a cost-effective modular monolith.

---

## 2. Ledgance Audit — objective

**Ledgance Audit is a professional audit management platform designed for audit teams,
organizations and firms to manage the complete audit lifecycle.**

It helps audit professionals manage:

- Clients
- Engagements
- Engagement teams
- Audit planning
- Materiality
- Risk assessment
- Audit procedures
- Working papers
- Evidence
- Findings
- Review workflows
- Audit reporting
- Audit history and activity

The platform reduces manual audit administration and provides structured, traceable workflows
for professional audit work.

**Audit is not a generic project-management application.** It must reflect genuine audit
workflows and concepts — materiality with a recorded basis, risks linked to assertions and
responses, working papers with preparer/reviewer sign-off, evidence with versioning and
cross-references, an immutable activity trail.

**There is no individual freelancer or solo-auditor subscription.** The offering is
organizational, beginning at Free.

---

## 3. Ledgance Accounting — objective

**Ledgance Accounting is a separate professional accounting platform designed to help
organizations manage their accounting operations and financial information.**

It supports:

- Chart of accounts
- Accounts
- Fiscal periods
- Transactions
- Journal entries
- General ledger
- Trial balance
- Reconciliation
- Financial reporting
- Accounting documents
- Accounting history and activity
- AI-assisted accounting analysis

Accounting remains a **separate bounded context** from Audit. Accounting business rules belong
to Accounting; Audit business rules belong to Audit. **Do not merge their domain models merely
because they are deployed together today.**

---

## 4. Audit ↔ Accounting relationship

The two platforms are conceptually separate, but an organization may subscribe to both.

When an organization uses both and has authorized the link, authorized Audit users may consume
appropriate Accounting context, such as:

- Trial balance
- General ledger
- Account balances
- Transactions
- Financial statements
- Reconciliations
- Adjustments
- Fiscal periods
- Supporting accounting documents

Rules:

1. Sharing occurs through an **explicit application/integration boundary**.
2. **Audit must not directly manipulate Accounting domain entities.** It consumes a read-only,
   permissioned projection expressed in Audit's own vocabulary.
3. Accounting remains responsible for Accounting rules; Audit for Audit rules.
4. Sharing is opt-in per organization and re-checked server-side on every call.
5. Every consumption is recorded in the Audit trail — an auditor must be able to show where a
   figure came from.

See `module-boundaries.md` §4 for the enforceable form of this boundary.

---

## 5. External accounting context

**Audit must work for organizations that do not use Ledgance Accounting.**

External accounting context may arrive through:

- CSV
- Excel
- Trial balance imports
- General ledger imports
- Financial statements
- Supporting documents
- Client-provided accounting information
- Information originating from external accounting systems

Therefore **using Ledgance Accounting is optional for Audit, and Audit must never require it.**
The external-file source is the baseline implementation; the Ledgance Accounting adapter is an
additional, optional source behind the same abstraction. Any Audit feature that only works when
Accounting is present is a design error.

---

## 6. AI strategy

Ledgance uses different AI providers depending on workload complexity and subscription
entitlement. Domain logic never names a provider.

| Provider | Intended for | Examples |
| --- | --- | --- |
| **Ollama** | Cost-effective workloads | Basic Q&A, simple summaries, basic classification, basic document analysis, lower-complexity assistance |
| **OpenAI** | Advanced / general workloads | Advanced analysis, more capable document understanding, drafting, more complex assistance |
| **Anthropic** | Complex reasoning, large context | Complex audit analysis, complex financial reasoning, large-context document analysis |
| **OpenClaw** | Agentic AI / orchestration | Multi-step workflows, tool orchestration |

Provider selection is based on **task complexity, capability, subscription entitlement, cost and
context requirements**. Do not automatically use the most expensive model for every request.

**Agents must interact through authorized application capabilities. Agents must never
manipulate the database directly.**

---

## 7. Audit AI use cases

**Basic** — audit assistant; engagement Q&A; document summarization; basic evidence
summarization; basic working-paper assistance.

**Intermediate** — risk suggestions; evidence analysis; working-paper drafting; finding
drafting; audit procedure assistance; cross-document analysis.

**Advanced** — complex risk analysis; complex evidence analysis; anomaly detection;
cross-document reasoning; review assistance; audit report drafting; complex engagement analysis.

**Agentic** — multi-step engagement analysis; authorized evidence investigation; cross-source
analysis; automated preparation workflows; AI-assisted review workflows.

AI output is **assistance**. It does not replace professional auditor judgment. AI must always
respect organization, client, engagement, user, role, permissions and subscription.

---

## 8. Accounting AI use cases

**Basic** — accounting assistant; accounting Q&A; transaction explanations; basic categorization
assistance; financial summaries.

**Intermediate** — journal-entry assistance; reconciliation assistance; financial statement
explanation; variance analysis; document analysis.

**Advanced** — financial anomaly detection; complex variance analysis; cross-document reasoning;
advanced financial analysis; complex accounting explanations.

**Agentic** — multi-step accounting analysis; authorized reconciliation workflows; financial
investigation; automated preparation assistance; AI-assisted accounting workflows.

AI must respect accounting rules and authorization, and must never bypass Accounting business
logic.

---

## 9. Audit subscriptions

| Plan | Users | Intent |
| --- | --- | --- |
| **Free** | up to 2 | 1 client, 2 engagements, limited AI. A genuinely useful entry experience with enough product value to encourage upgrading. |
| **Professional** | up to 30 | Expanded audit capabilities, more clients and engagements, more collaboration, increased AI capability and usage, advanced audit workflows. |
| **Organization** | up to 75 | Higher organizational capacity, advanced collaboration, higher AI usage, advanced audit capabilities, more automation. |
| **Firm** | up to 150 | Large-team capability, advanced collaboration, high AI usage, advanced workflows, more automation and organizational capabilities. |
| **Enterprise** | Contact Sales | Enterprise-scale requirements, custom commercial arrangements, highest capacity, highest AI capability. |

Feature entitlements must remain **centralized**. Do not scatter plan-specific logic through the
application.

---

## 10. Accounting subscriptions

| Plan | Price | Intent |
| --- | --- | --- |
| **Free** | — | Limited but genuinely useful accounting functionality, limited AI. Demonstrates real product value and encourages natural upgrade. |
| **Solo** | $14.99 / month | An individual accounting user; expanded accounting capabilities, increased AI capability and usage. |
| **Team** | not yet defined | Collaborative accounting, multiple users, higher limits, increased AI usage, collaboration features. |
| **Professional** | not yet defined | Professional accounting capabilities, larger teams, advanced accounting functionality, advanced AI, increased automation. |
| **Enterprise** | Contact Sales | Enterprise-scale capabilities, custom requirements, highest limits, highest AI capabilities. |

**Solo is the only Accounting plan with defined pricing.** Do not invent prices for the others.

---

## 11. Free plan product strategy

Both Free plans must be **useful**. The objective is not to make them unusable.

A Free user should be able to complete a real workflow end to end — a real engagement in Audit,
a real period in Accounting — and experience genuine product value, including basic AI.

Upgrade reasons should arise naturally from: higher limits, more users, more clients, more
engagements, more collaboration, advanced functionality, higher AI usage, better AI
capabilities, automation, professional workflows.

**The upgrade experience should feel natural rather than manipulative.** Never wall off the
middle of a core workflow.

---

## 12. AI subscription strategy

AI capability is **entitlement-driven** and progressive:

| Tier | AI |
| --- | --- |
| **Free** | Basic AI, limited usage, lower-cost models where appropriate |
| **Lower paid tiers** | Increased usage, more capable models, more advanced document/context handling |
| **Professional / Organization / Firm** | Advanced AI, larger context, more complex reasoning, higher usage, more automation |
| **Enterprise** | Highest capability and usage, advanced and complex workflows, enterprise requirements |

The exact feature matrix may evolve. **Centralize AI entitlements**; do not hardcode plan
assumptions into unrelated features.

---

## 13. Architectural rules

Permanent. Also summarized in `CLAUDE.md`.

**Use:** Domain-Driven Design · Vertical Slice Architecture · Modular Monolith · explicit
Bounded Contexts · the custom Mediator in `Ledgance.Shared` · .NET 10 · Next.js · React ·
TypeScript · Supabase with the official Supabase C# package · xUnit.

**Do not use:** Entity Framework Core · MediatR · any other mediator implementation.

**Also:** do not duplicate existing shared infrastructure; reuse the existing UI components;
keep domain boundaries explicit; enforce authorization and organization isolation server-side;
never expose backend secrets to the frontend.

---

## 14. Where to look next

| Question | Document |
| --- | --- |
| What are the permanent working rules? | `../CLAUDE.md` |
| Where is the implementation right now? | `project-state.md` |
| Which phases are done? | `implementation-status.md` |
| How is the system built? | `architecture.md` |
| What may reference what? | `module-boundaries.md` |
| How do entitlements resolve? | `subscription-entitlements.md` |
| What AI exists versus is planned? | `ai-architecture.md` |
| Why was something decided? | `decisions.md` |
| What is the MVP capability scope? | `product-requirements.md` |
