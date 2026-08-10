import Link from "next/link";
import {
  ArrowRight,
  BookOpen,
  Bot,
  Calculator,
  CheckCircle2,
  ClipboardCheck,
  FileSpreadsheet,
  GitBranch,
  Link2,
  Lock,
  Scale,
  ShieldCheck,
  Sparkles,
  Users,
} from "lucide-react";
import { MarketingHeader } from "@/components/marketing-header";
import { MarketingFooter } from "@/components/marketing-footer";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

const accountingHighlights = [
  "Double-entry journal with draft, post and reverse",
  "Chart of accounts, fiscal periods and reconciliation",
  "Live trial balance, income statement and balance sheet",
  "AI that explains, suggests and investigates your books",
];

const auditHighlights = [
  "Clients, engagements, teams and planning with materiality",
  "Working papers with structured sign-offs and review notes",
  "Versioned evidence, findings and report drafting",
  "AI that suggests risks, drafts papers and assists review",
];

const securityPoints = [
  {
    icon: Users,
    title: "Organization isolation",
    desc: "Every record is scoped to your organization at three enforcement layers, including database row-level security.",
  },
  {
    icon: Lock,
    title: "Server-side authorization",
    desc: "Roles and permissions are decided on the server for every request — the interface only renders what you may do.",
  },
  {
    icon: GitBranch,
    title: "Append-only activity trail",
    desc: "Every material change is recorded with who, what and when. Posted accounting entries are immutable by design.",
  },
  {
    icon: Bot,
    title: "AI under control",
    desc: "AI reads only what you could already read, its output is always a reviewable proposal, and agents act through authorized application capabilities only.",
  },
];

export default function Home() {
  return (
    <div className="min-h-screen bg-ambient">
      <MarketingHeader />
      <main>
        {/* Hero */}
        <section className="relative overflow-hidden">
          <div className="pointer-events-none absolute inset-0 bg-grid opacity-[0.15]" />
          <div className="pointer-events-none absolute left-1/3 top-0 -z-10 h-[600px] w-[600px] -translate-x-1/2 rounded-full bg-emerald-500/10 blur-[120px]" />
          <div className="pointer-events-none absolute right-0 top-40 -z-10 h-[400px] w-[400px] rounded-full bg-sky-500/10 blur-[100px]" />
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-3xl text-center">
              <div className="mb-6 flex justify-center animate-fade-in-up">
                <Badge
                  variant="secondary"
                  className="gap-1.5 rounded-full border border-border/60 bg-background/80 px-3 py-1 text-xs font-medium backdrop-blur"
                >
                  <Sparkles className="h-3.5 w-3.5 text-primary" />
                  Two platforms · one professional ecosystem
                </Badge>
              </div>
              <h1 className="animate-fade-in-up font-display text-4xl font-bold tracking-tight text-balance delay-100 sm:text-5xl lg:text-6xl">
                Professional{" "}
                <span className="bg-gradient-to-r from-emerald-500 to-teal-500 bg-clip-text text-transparent">
                  accounting
                </span>{" "}
                and{" "}
                <span className="bg-gradient-to-r from-sky-500 to-indigo-500 bg-clip-text text-transparent">
                  audit
                </span>
                , done right
              </h1>
              <p className="mx-auto mt-6 max-w-2xl animate-fade-in-up text-lg leading-relaxed text-muted-foreground text-balance delay-200">
                Ledgance is two distinct products: Ledgance Accounting for real
                double-entry bookkeeping, and Ledgance Audit for the complete
                audit lifecycle. Pick the one you need — each stands entirely on
                its own.
              </p>
              <div className="mt-8 flex animate-fade-in-up flex-col items-center justify-center gap-4 delay-300 sm:flex-row">
                <Link href="/#choose">
                  <Button size="lg" className="h-12 px-8 text-base font-semibold">
                    Choose your platform
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
                <Link href="/pricing">
                  <Button
                    variant="outline"
                    size="lg"
                    className="h-12 px-8 text-base font-semibold"
                  >
                    View plans
                  </Button>
                </Link>
              </div>
              <p className="mt-4 animate-fade-in-up text-sm text-muted-foreground delay-400">
                Both platforms include a genuinely useful free plan
              </p>
            </div>
          </div>
        </section>

        {/* Platform chooser */}
        <section id="choose" className="scroll-mt-20 border-y border-border/60 bg-muted/20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-24">
            <div className="mx-auto max-w-2xl text-center">
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                What do you need Ledgance for?
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Accounting and Audit are separate products with their own plans.
                You are never required to subscribe to both.
              </p>
            </div>
            <div className="mt-14 grid gap-8 lg:grid-cols-2">
              {/* Accounting card */}
              <div className="group relative flex flex-col overflow-hidden rounded-3xl border border-border/60 bg-card p-8 transition-all hover:border-emerald-500/40 hover:shadow-xl hover:shadow-emerald-500/5 lg:p-10">
                <div className="pointer-events-none absolute right-0 top-0 h-40 w-40 rounded-full bg-emerald-500/10 blur-3xl" />
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-50 dark:bg-emerald-950/40">
                  <Calculator className="h-6 w-6 text-emerald-500" />
                </div>
                <h3 className="mt-6 font-display text-2xl font-bold">
                  Ledgance Accounting
                </h3>
                <p className="mt-1 text-sm font-medium text-emerald-600 dark:text-emerald-400">
                  I keep the books
                </p>
                <p className="mt-4 leading-relaxed text-muted-foreground">
                  Run real double-entry books for one entity or many: journal
                  entries, fiscal periods, reconciliation, live financial
                  statements — with AI assistance built into the workflow.
                </p>
                <ul className="mt-6 space-y-3">
                  {accountingHighlights.map((item) => (
                    <li key={item} className="flex items-start gap-3">
                      <CheckCircle2 className="mt-0.5 h-4.5 w-4.5 flex-shrink-0 text-emerald-500" />
                      <span className="text-sm">{item}</span>
                    </li>
                  ))}
                </ul>
                <div className="mt-8 flex flex-1 items-end gap-3">
                  <Link href="/signup?platform=accounting" className="flex-1">
                    <Button className="w-full bg-emerald-600 font-semibold hover:bg-emerald-700">
                      Try Accounting free
                    </Button>
                  </Link>
                  <Link href="/accounting" className="flex-1">
                    <Button variant="outline" className="w-full font-semibold">
                      Explore Accounting
                    </Button>
                  </Link>
                </div>
              </div>

              {/* Audit card */}
              <div className="group relative flex flex-col overflow-hidden rounded-3xl border border-border/60 bg-card p-8 transition-all hover:border-sky-500/40 hover:shadow-xl hover:shadow-sky-500/5 lg:p-10">
                <div className="pointer-events-none absolute right-0 top-0 h-40 w-40 rounded-full bg-sky-500/10 blur-3xl" />
                <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-sky-50 dark:bg-sky-950/40">
                  <ClipboardCheck className="h-6 w-6 text-sky-500" />
                </div>
                <h3 className="mt-6 font-display text-2xl font-bold">
                  Ledgance Audit
                </h3>
                <p className="mt-1 text-sm font-medium text-sky-600 dark:text-sky-400">
                  I audit the books
                </p>
                <p className="mt-4 leading-relaxed text-muted-foreground">
                  Manage the full audit lifecycle: engagements and teams,
                  planning and risk, procedures, working papers with sign-offs,
                  versioned evidence, findings and reports.
                </p>
                <ul className="mt-6 space-y-3">
                  {auditHighlights.map((item) => (
                    <li key={item} className="flex items-start gap-3">
                      <CheckCircle2 className="mt-0.5 h-4.5 w-4.5 flex-shrink-0 text-sky-500" />
                      <span className="text-sm">{item}</span>
                    </li>
                  ))}
                </ul>
                <div className="mt-8 flex flex-1 items-end gap-3">
                  <Link href="/signup?platform=audit" className="flex-1">
                    <Button className="w-full bg-sky-600 font-semibold hover:bg-sky-700">
                      Try Audit free
                    </Button>
                  </Link>
                  <Link href="/audit" className="flex-1">
                    <Button variant="outline" className="w-full font-semibold">
                      Explore Audit
                    </Button>
                  </Link>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Ecosystem — better together, never required */}
        <section id="ecosystem" className="scroll-mt-20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="grid items-center gap-12 lg:grid-cols-2">
              <div>
                <Badge variant="secondary" className="mb-4">
                  One ecosystem
                </Badge>
                <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                  Independent by design. Better when connected.
                </h2>
                <p className="mt-4 text-lg leading-relaxed text-muted-foreground">
                  Neither platform requires the other. Audit works with trial
                  balances imported from any accounting system, and Accounting
                  never needs an auditor in the room. But when one organization
                  uses both, they can connect.
                </p>
                <div className="mt-8 space-y-4">
                  <div className="flex items-start gap-4">
                    <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-primary/10">
                      <Link2 className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                      <h4 className="font-semibold">
                        Authorized context sharing
                      </h4>
                      <p className="text-sm text-muted-foreground">
                        An admin can allow Audit to read the organization&apos;s
                        own Accounting books — entities, fiscal periods and
                        trial balances — with every import recorded in the audit
                        trail.
                      </p>
                    </div>
                  </div>
                  <div className="flex items-start gap-4">
                    <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-primary/10">
                      <FileSpreadsheet className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                      <h4 className="font-semibold">
                        External context always works
                      </h4>
                      <p className="text-sm text-muted-foreground">
                        No Ledgance Accounting? Audit imports client-provided
                        trial balances from CSV exports of any accounting
                        system. Nothing about Audit depends on Accounting.
                      </p>
                    </div>
                  </div>
                  <div className="flex items-start gap-4">
                    <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-primary/10">
                      <Scale className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                      <h4 className="font-semibold">Your choice, always</h4>
                      <p className="text-sm text-muted-foreground">
                        Subscribe to one platform, the other, or both. The
                        connection is opt-in per organization and can be turned
                        off at any time.
                      </p>
                    </div>
                  </div>
                </div>
              </div>
              <div className="relative">
                <div className="pointer-events-none absolute inset-0 rounded-3xl bg-gradient-to-br from-emerald-500/10 to-sky-500/10 blur-2xl" />
                <div className="relative overflow-hidden rounded-3xl border border-border/60 bg-card p-8">
                  <div className="flex items-center justify-between gap-4">
                    <div className="flex flex-1 flex-col items-center rounded-2xl border border-emerald-500/30 bg-emerald-50/50 p-5 text-center dark:bg-emerald-950/20">
                      <BookOpen className="h-7 w-7 text-emerald-500" />
                      <div className="mt-2 font-display text-sm font-bold">
                        Accounting
                      </div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        The books
                      </div>
                    </div>
                    <div className="flex flex-col items-center gap-1 text-muted-foreground">
                      <Link2 className="h-5 w-5 text-primary" />
                      <span className="text-[10px] font-medium uppercase tracking-wide">
                        optional
                      </span>
                    </div>
                    <div className="flex flex-1 flex-col items-center rounded-2xl border border-sky-500/30 bg-sky-50/50 p-5 text-center dark:bg-sky-950/20">
                      <ShieldCheck className="h-7 w-7 text-sky-500" />
                      <div className="mt-2 font-display text-sm font-bold">
                        Audit
                      </div>
                      <div className="mt-1 text-xs text-muted-foreground">
                        The assurance
                      </div>
                    </div>
                  </div>
                  <div className="mt-6 space-y-3">
                    {[
                      "Trial balances flow into engagements with provenance",
                      "Sharing requires both plans to include it — and an admin to enable it",
                      "Every consumption is recorded in the audit trail",
                    ].map((line) => (
                      <div
                        key={line}
                        className="flex items-center gap-3 rounded-lg border border-border/60 bg-background px-4 py-3"
                      >
                        <CheckCircle2 className="h-4.5 w-4.5 flex-shrink-0 text-primary" />
                        <span className="text-sm">{line}</span>
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Security */}
        <section id="security" className="scroll-mt-20 bg-muted/20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Security by design
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                Built for work that has to stand up to scrutiny
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Financial records and audit files demand more than a generic SaaS
                security page. This is how Ledgance is actually built.
              </p>
            </div>
            <div className="mt-16 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
              {securityPoints.map((point) => (
                <div
                  key={point.title}
                  className="rounded-2xl border border-border/60 bg-card p-6"
                >
                  <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
                    <point.icon className="h-5 w-5 text-primary" />
                  </div>
                  <h3 className="mt-4 font-semibold">{point.title}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                    {point.desc}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* CTA */}
        <section className="relative overflow-hidden">
          <div className="pointer-events-none absolute inset-0 bg-gradient-to-br from-emerald-600 via-teal-600 to-sky-600" />
          <div className="pointer-events-none absolute inset-0 bg-grid opacity-10" />
          <div className="relative mx-auto max-w-4xl px-6 py-20 text-center lg:py-28">
            <h2 className="font-display text-3xl font-bold tracking-tight text-white text-balance sm:text-4xl lg:text-5xl">
              Start with the platform you actually need
            </h2>
            <p className="mx-auto mt-4 max-w-2xl text-lg text-white/80 text-balance">
              Free plans on both sides — real functionality, not a demo. Upgrade
              when your practice grows.
            </p>
            <div className="mt-8 flex flex-col items-center justify-center gap-4 sm:flex-row">
              <Link href="/signup?platform=accounting">
                <Button
                  size="lg"
                  className="h-12 bg-white px-8 text-base font-semibold text-emerald-700 hover:bg-white/90"
                >
                  <Calculator className="mr-2 h-4 w-4" />
                  Try Accounting
                </Button>
              </Link>
              <Link href="/signup?platform=audit">
                <Button
                  size="lg"
                  className="h-12 bg-white px-8 text-base font-semibold text-sky-700 hover:bg-white/90"
                >
                  <ClipboardCheck className="mr-2 h-4 w-4" />
                  Try Audit
                </Button>
              </Link>
              <Link href="/pricing">
                <Button
                  size="lg"
                  variant="outline"
                  className="h-12 border-white/30 bg-white/10 px-8 text-base font-semibold text-white hover:bg-white/20 hover:text-white"
                >
                  View plans
                </Button>
              </Link>
            </div>
          </div>
        </section>
      </main>
      <MarketingFooter />
    </div>
  );
}
