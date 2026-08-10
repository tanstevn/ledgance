import Link from "next/link";
import type { Metadata } from "next";
import {
  ArrowRight,
  Bot,
  Building2,
  CheckCircle2,
  ClipboardCheck,
  FileSearch,
  FileSpreadsheet,
  GitBranch,
  Link2,
  ShieldCheck,
  Sparkles,
  Target,
} from "lucide-react";
import { MarketingHeader } from "@/components/marketing-header";
import { MarketingFooter } from "@/components/marketing-footer";
import { PricingPlans } from "@/components/pricing-plans";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

export const metadata: Metadata = {
  title: "Ledgance Audit — the complete audit lifecycle",
  description:
    "Engagements, planning, risk, procedures, working papers with sign-offs, versioned evidence, findings and reports — with AI assistance throughout.",
};

const features = [
  {
    icon: Building2,
    title: "Clients & engagements",
    description:
      "Manage your client portfolio and every engagement's type, period, budget and status — with a team model that confines access to assigned members.",
  },
  {
    icon: Target,
    title: "Planning, materiality & risk",
    description:
      "Document scope, objectives and strategy, set materiality with performance thresholds, and record risks with assertions and planned responses.",
  },
  {
    icon: ClipboardCheck,
    title: "Working papers with sign-offs",
    description:
      "Prepare, review and approve with enforced preparer/reviewer segregation, structured review notes, and status that reflects reality.",
  },
  {
    icon: GitBranch,
    title: "Versioned evidence",
    description:
      "Upload evidence with full version history — supersede, never overwrite — linked to procedures and working papers with a complete trail.",
  },
  {
    icon: FileSearch,
    title: "Findings & reports",
    description:
      "Raise findings with severity and recommendations, track resolution, and build the audit report from the engagement's actual results.",
  },
  {
    icon: FileSpreadsheet,
    title: "Trial balance, your way",
    description:
      "Import the client's trial balance from any system via CSV — or, when your organization also runs Ledgance Accounting, read it directly with provenance.",
  },
];

const aiCapabilities = [
  { tier: "Free", items: ["Audit assistant & engagement Q&A", "Working paper and evidence summarization"] },
  { tier: "Professional", items: ["Risk & procedure suggestions", "Working paper drafting", "Finding drafting from observations"] },
  { tier: "Organization", items: ["Cross-document risk analysis", "Trial balance anomaly detection", "Review assistance & report drafting"] },
  { tier: "Firm & Enterprise", items: ["Agentic AI that investigates the engagement step by step through authorized, read-only tools"] },
];

export default function AuditPage() {
  return (
    <div className="min-h-screen bg-ambient">
      <MarketingHeader />
      <main>
        {/* Hero */}
        <section className="relative overflow-hidden">
          <div className="pointer-events-none absolute inset-0 bg-grid opacity-[0.15]" />
          <div className="pointer-events-none absolute left-1/2 top-0 -z-10 h-[500px] w-[500px] -translate-x-1/2 rounded-full bg-sky-500/10 blur-[120px]" />
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-3xl text-center">
              <div className="mb-6 flex justify-center">
                <Badge
                  variant="secondary"
                  className="gap-1.5 rounded-full border border-border/60 bg-background/80 px-3 py-1 text-xs font-medium backdrop-blur"
                >
                  <ShieldCheck className="h-3.5 w-3.5 text-sky-500" />
                  Ledgance Audit
                </Badge>
              </div>
              <h1 className="font-display text-4xl font-bold tracking-tight text-balance sm:text-5xl lg:text-6xl">
                The audit file that{" "}
                <span className="bg-gradient-to-r from-sky-500 to-indigo-500 bg-clip-text text-transparent">
                  reviews itself in
                </span>
              </h1>
              <p className="mx-auto mt-6 max-w-2xl text-lg leading-relaxed text-muted-foreground text-balance">
                From engagement setup to final report: planning, risk,
                procedures, working papers with real sign-off discipline,
                versioned evidence and findings — one engagement file, always
                review-ready.
              </p>
              <div className="mt-8 flex flex-col items-center justify-center gap-4 sm:flex-row">
                <Link href="/signup?platform=audit">
                  <Button
                    size="lg"
                    className="h-12 bg-sky-600 px-8 text-base font-semibold hover:bg-sky-700"
                  >
                    Start free
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
                <Link href="/pricing?platform=audit">
                  <Button
                    variant="outline"
                    size="lg"
                    className="h-12 px-8 text-base font-semibold"
                  >
                    View Audit plans
                  </Button>
                </Link>
              </div>
              <p className="mt-4 text-sm text-muted-foreground">
                Free includes 1 client, 2 engagements and AI assistance — no
                card required
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
                The whole lifecycle, not just a document store
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Built around how audit teams actually work — team confinement,
                segregation of duties and an append-only trail included.
              </p>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-2 lg:grid-cols-3">
              {features.map((feature) => (
                <div
                  key={feature.title}
                  className="group rounded-2xl border border-border/60 bg-card p-8 transition-all hover:border-sky-500/30 hover:shadow-lg hover:shadow-sky-500/5"
                >
                  <div className="flex h-12 w-12 items-center justify-center rounded-xl bg-sky-50 dark:bg-sky-950/40">
                    <feature.icon className="h-6 w-6 text-sky-500" />
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
                <Sparkles className="h-3.5 w-3.5 text-sky-500" />
                Audit AI
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                Proposals for the team. Judgment stays with you.
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                AI is confined to your engagement team&apos;s access, its output
                is always a reviewable proposal, and nothing enters the audit
                record without a human decision.
              </p>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-2 lg:grid-cols-4">
              {aiCapabilities.map((group) => (
                <div
                  key={group.tier}
                  className="rounded-2xl border border-border/60 bg-card p-6"
                >
                  <div className="flex items-center gap-2">
                    <Bot className="h-4.5 w-4.5 text-sky-500" />
                    <span className="text-sm font-semibold">{group.tier}</span>
                  </div>
                  <ul className="mt-4 space-y-3">
                    {group.items.map((item) => (
                      <li key={item} className="flex items-start gap-2.5">
                        <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0 text-sky-500" />
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

        {/* Accounting connection */}
        <section className="border-y border-border/60 bg-muted/20">
          <div className="mx-auto max-w-4xl px-6 py-16 text-center">
            <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10">
              <Link2 className="h-6 w-6 text-primary" />
            </div>
            <h2 className="mt-6 font-display text-2xl font-bold tracking-tight text-balance sm:text-3xl">
              Works with any accounting system. Connects to one.
            </h2>
            <p className="mx-auto mt-4 max-w-2xl text-muted-foreground text-balance">
              Audit imports client trial balances from CSV exports of any
              system — that is the baseline, and it always works. When your
              organization also runs Ledgance Accounting, authorized sharing
              lets the engagement read the books directly, with every import
              stamped with its source.
            </p>
            <Link
              href="/accounting"
              className="mt-6 inline-flex items-center gap-1 text-sm font-medium text-primary hover:underline"
            >
              Learn about Ledgance Accounting
              <ArrowRight className="h-4 w-4" />
            </Link>
          </div>
        </section>

        {/* Pricing */}
        <section id="pricing" className="scroll-mt-20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Audit plans
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                From first engagement to firm scale
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Every limit shown here is the same limit the platform enforces —
                no fine print.
              </p>
            </div>
            <div className="mt-16">
              <PricingPlans platform="audit" />
            </div>
          </div>
        </section>
      </main>
      <MarketingFooter />
    </div>
  );
}
