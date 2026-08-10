import Link from "next/link";
import type { Metadata } from "next";
import {
  ArrowRight,
  Bot,
  Building2,
  Calculator,
  CheckCircle2,
  FileSpreadsheet,
  FileText,
  Landmark,
  Link2,
  Scale,
  Sparkles,
} from "lucide-react";
import { MarketingHeader } from "@/components/marketing-header";
import { MarketingFooter } from "@/components/marketing-footer";
import { PricingPlans } from "@/components/pricing-plans";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

export const metadata: Metadata = {
  title: "Ledgance Accounting — professional double-entry bookkeeping",
  description:
    "Real double-entry books: journal entries, fiscal periods, reconciliation, live financial statements and built-in AI assistance.",
};

const features = [
  {
    icon: Building2,
    title: "Entities & separate books",
    description:
      "Keep books for one entity or many, each with its own base currency, chart of accounts and fiscal calendar — cleanly separated, never mixed.",
  },
  {
    icon: Landmark,
    title: "Chart of accounts that behaves",
    description:
      "Typed accounts with natural debit/credit balances, hierarchical sub-accounts, and rules that stop postings to summary or inactive accounts.",
  },
  {
    icon: Scale,
    title: "True double-entry journal",
    description:
      "Entries must balance before they post. Draft freely, post into open fiscal periods, and correct posted entries only by reversal — the record stays honest.",
  },
  {
    icon: FileSpreadsheet,
    title: "Live ledger & statements",
    description:
      "General ledger with running balances, a trial balance that always ties, income statement and balance sheet — derived live from your postings, never stale.",
  },
  {
    icon: CheckCircle2,
    title: "Reconciliation with discipline",
    description:
      "Reconcile any account against its statement, clear lines one by one, and close only when the difference is zero — or explicitly explained.",
  },
  {
    icon: FileText,
    title: "Source documents & history",
    description:
      "Attach invoices and receipts to entries and reconciliations, and see every change in an append-only activity history for each entity.",
  },
];

const aiCapabilities = [
  { tier: "Free", items: ["Accounting assistant & entity Q&A", "Plain-language journal entry explanations", "Period financial summaries"] },
  { tier: "Solo & Team", items: ["Journal entry suggestions from a described transaction", "Reconciliation assistance", "Statement explanation & variance analysis"] },
  { tier: "Professional", items: ["Anomaly detection across the ledger", "Complex financial analysis", "Period-close review"] },
  { tier: "Enterprise", items: ["Agentic AI that investigates your books step by step through authorized, read-only tools"] },
];

export default function AccountingPage() {
  return (
    <div className="min-h-screen bg-ambient">
      <MarketingHeader />
      <main>
        {/* Hero */}
        <section className="relative overflow-hidden">
          <div className="pointer-events-none absolute inset-0 bg-grid opacity-[0.15]" />
          <div className="pointer-events-none absolute left-1/2 top-0 -z-10 h-[500px] w-[500px] -translate-x-1/2 rounded-full bg-emerald-500/10 blur-[120px]" />
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-3xl text-center">
              <div className="mb-6 flex justify-center">
                <Badge
                  variant="secondary"
                  className="gap-1.5 rounded-full border border-border/60 bg-background/80 px-3 py-1 text-xs font-medium backdrop-blur"
                >
                  <Calculator className="h-3.5 w-3.5 text-emerald-500" />
                  Ledgance Accounting
                </Badge>
              </div>
              <h1 className="font-display text-4xl font-bold tracking-tight text-balance sm:text-5xl lg:text-6xl">
                Bookkeeping that{" "}
                <span className="bg-gradient-to-r from-emerald-500 to-teal-500 bg-clip-text text-transparent">
                  respects the rules
                </span>
              </h1>
              <p className="mx-auto mt-6 max-w-2xl text-lg leading-relaxed text-muted-foreground text-balance">
                Real double-entry accounting for professionals: balanced journal
                entries, disciplined periods, honest reconciliation and
                financial statements that always tie — with AI assistance woven
                into the workflow.
              </p>
              <div className="mt-8 flex flex-col items-center justify-center gap-4 sm:flex-row">
                <Link href="/signup?platform=accounting">
                  <Button
                    size="lg"
                    className="h-12 bg-emerald-600 px-8 text-base font-semibold hover:bg-emerald-700"
                  >
                    Start free
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
                <Link href="/pricing?platform=accounting">
                  <Button
                    variant="outline"
                    size="lg"
                    className="h-12 px-8 text-base font-semibold"
                  >
                    View Accounting plans
                  </Button>
                </Link>
              </div>
              <p className="mt-4 text-sm text-muted-foreground">
                Free includes 1 entity, 300 transactions per period and AI
                assistance — no card required
              </p>
            </div>
          </div>
        </section>

        {/* Features */}
        <section className="border-y border-border/60 bg-muted/20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                The platform
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                Everything a real set of books needs
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Not an expense tracker with a ledger bolted on — an accounting
                system built around the rules accountants actually work by.
              </p>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
              {features.map((feature) => (
                <div
                  key={feature.title}
                  className="group rounded-2xl border border-border/60 bg-card p-8 transition-all hover:border-emerald-500/30 hover:shadow-lg hover:shadow-emerald-500/5"
                >
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-emerald-50 dark:bg-emerald-950/40">
                    <feature.icon className="h-6 w-6 text-emerald-500" />
                  </div>
                  <h3 className="mt-5 font-display text-lg font-semibold">
                    {feature.title}
                  </h3>
                  <p className="mt-3 text-sm leading-relaxed text-muted-foreground">
                    {feature.description}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* AI */}
        <section>
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4 gap-1.5">
                <Sparkles className="h-3.5 w-3.5 text-emerald-500" />
                Accounting AI
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                AI that assists your judgment — never replaces it
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Every AI capability works from your actual books, respects your
                permissions, and returns a proposal you review — nothing is ever
                written to the ledger without you.
              </p>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-2 lg:grid-cols-4">
              {aiCapabilities.map((group) => (
                <div
                  key={group.tier}
                  className="rounded-2xl border border-border/60 bg-card p-6"
                >
                  <div className="flex items-center gap-2">
                    <Bot className="h-4.5 w-4.5 text-emerald-500" />
                    <span className="text-sm font-semibold">
                      {group.tier}
                    </span>
                  </div>
                  <ul className="mt-4 space-y-3">
                    {group.items.map((item) => (
                      <li key={item} className="flex items-start gap-2.5">
                        <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0 text-emerald-500" />
                        <span className="text-sm text-muted-foreground">
                          {item}
                        </span>
                      </li>
                    ))}
                  </ul>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Audit connection */}
        <section className="border-y border-border/60 bg-muted/20">
          <div className="mx-auto max-w-4xl px-6 py-16 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10">
              <Link2 className="h-6 w-6 text-primary" />
            </div>
            <h2 className="mt-6 font-display text-2xl font-bold tracking-tight text-balance sm:text-3xl">
              Being audited? Your books are already audit-ready.
            </h2>
            <p className="mx-auto mt-4 max-w-2xl text-muted-foreground text-balance">
              If your organization also uses Ledgance Audit, an admin can allow
              the audit team to read your entities, periods and trial balances
              directly — no exports, full provenance. Entirely optional, and
              Accounting is complete without it.
            </p>
            <Link
              href="/audit"
              className="mt-6 inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
            >
              Learn about Ledgance Audit
              <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
        </section>

        {/* Pricing */}
        <section id="pricing" className="scroll-mt-20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Accounting plans
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                Start free. Upgrade when the books grow.
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Every limit shown here is the same limit the platform enforces —
                no fine print.
              </p>
            </div>
            <div className="mt-16">
              <PricingPlans platform="accounting" />
            </div>
          </div>
        </section>
      </main>
      <MarketingFooter />
    </div>
  );
}
