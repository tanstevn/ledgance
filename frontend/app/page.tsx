import Link from "next/link";
import {
  ShieldCheck,
  Building2,
  FileText,
  GitBranch,
  CheckCircle2,
  ArrowRight,
  Lock,
  Users,
  ClipboardCheck,
  FileSpreadsheet,
  Sparkles,
  BarChart3,
  Layers,
  Search,
  Bell,
  Calendar,
  Star,
} from "lucide-react";
import { MarketingHeader } from "@/components/marketing-header";
import { MarketingFooter } from "@/components/marketing-footer";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";

const features = [
  {
    icon: Building2,
    title: "Client & Engagement Management",
    description:
      "Track every client, engagement, and team assignment. Manage budgets, deadlines, and progress across your entire portfolio from a single command center.",
    color: "text-sky-500",
    bg: "bg-sky-50 dark:bg-sky-950/40",
  },
  {
    icon: GitBranch,
    title: "Document & Evidence Versioning",
    description:
      "Every piece of audit evidence is versioned and traceable. Upload, supersede, and compare document versions with a full audit trail.",
    color: "text-emerald-500",
    bg: "bg-emerald-50 dark:bg-emerald-950/40",
  },
  {
    icon: ClipboardCheck,
    title: "Working Papers with Sign-offs",
    description:
      "Prepare, review, and approve working papers with structured sign-off workflows. Add review notes, clear comments, and cross-reference lead sheets.",
    color: "text-amber-500",
    bg: "bg-amber-50 dark:bg-amber-950/40",
  },
  {
    icon: FileSpreadsheet,
    title: "Trial Balance & Account Mapping",
    description:
      "Import trial balances in seconds. Map accounts to financial statement line items, assign assertions, and flag risk areas — all with intelligent suggestions.",
    color: "text-violet-500",
    bg: "bg-violet-50 dark:bg-violet-950/40",
  },
];

const workflow = [
  {
    icon: Building2,
    step: "01",
    title: "Set up your engagement",
    description:
      "Create a client, define the engagement scope, assign your team, and set the budget and timeline.",
  },
  {
    icon: FileSpreadsheet,
    step: "02",
    title: "Import the trial balance",
    description:
      "Upload the GL trial balance. Ledgance maps accounts to financial statement line items and flags high-risk areas.",
  },
  {
    icon: FileText,
    step: "03",
    title: "Gather evidence",
    description:
      "Upload confirmations, statements, and supporting docs. Every file is versioned with a complete change history.",
  },
  {
    icon: ClipboardCheck,
    step: "04",
    title: "Prepare working papers",
    description:
      "Build lead sheets, document conclusions, cross-reference supporting papers, and submit for review.",
  },
  {
    icon: CheckCircle2,
    step: "05",
    title: "Review and sign off",
    description:
      "Managers review, leave notes, and approve. Partners give final sign-off. The engagement file is complete and archived.",
  },
];

const stats = [
  { value: "40%", label: "Faster fieldwork" },
  { value: "100%", label: "Versioned evidence" },
  { value: "5x", label: "Review throughput" },
  { value: "SOC 2", label: "Type II certified" },
];

const testimonials = [
  {
    quote:
      "Ledgance replaced three separate tools for us. Working paper sign-offs that used to take a week now happen in a day.",
    author: "Sarah Whitman",
    role: "Audit Partner, Northgate Advisory",
    initials: "SW",
  },
  {
    quote:
      "The trial balance mapping alone saved my team dozens of hours this season. Account mapping suggestions are remarkably accurate.",
    author: "David Cho",
    role: "Partner, Meridian Audit Partners",
    initials: "DC",
  },
  {
    quote:
      "Document versioning means we never lose track of which evidence supports which conclusion. The audit trail is impeccable.",
    author: "Maya Singh",
    role: "Senior Manager, Meridian Audit Partners",
    initials: "MS",
  },
];

const pricingPlans = [
  {
    name: "Starter",
    price: "$89",
    period: "/user/mo",
    description: "For small firms getting started with digital audits.",
    features: [
      "Up to 10 active engagements",
      "2 organizations",
      "Document versioning",
      "Working paper sign-offs",
      "Email support",
    ],
    cta: "Start free trial",
    highlighted: false,
  },
  {
    name: "Professional",
    price: "$199",
    period: "/user/mo",
    description: "For growing firms managing multiple clients.",
    features: [
      "Unlimited engagements",
      "5 organizations",
      "Advanced trial balance mapping",
      "Review notes & cross-references",
      "Custom assertions",
      "Priority support",
    ],
    cta: "Start free trial",
    highlighted: true,
  },
  {
    name: "Enterprise",
    price: "Custom",
    period: "",
    description: "For large firms with advanced needs.",
    features: [
      "Everything in Professional",
      "Unlimited organizations",
      "SSO & SCIM provisioning",
      "Custom workflows & templates",
      "Dedicated success manager",
      "On-premise deployment option",
    ],
    cta: "Contact sales",
    highlighted: false,
  },
];

export default function Home() {
  return (
    <div className="min-h-screen bg-background">
      <MarketingHeader />
      <main>
        <section className="relative overflow-hidden">
          <div className="absolute inset-0 bg-grid opacity-[0.15]" />
          <div className="absolute left-1/2 top-0 -z-10 h-[600px] w-[600px] -translate-x-1/2 rounded-full bg-primary/10 blur-[120px]" />
          <div className="absolute right-0 top-40 -z-10 h-[400px] w-[400px] rounded-full bg-emerald-500/10 blur-[100px]" />
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-3xl text-center">
              <div className="mb-6 flex justify-center animate-fade-in-up">
                <Badge
                  variant="secondary"
                  className="gap-1.5 rounded-full border border-border/60 bg-background/80 px-3 py-1 text-xs font-medium backdrop-blur"
                >
                  <Sparkles className="h-3.5 w-3.5 text-primary" />
                  Now with AI-assisted account mapping
                </Badge>
              </div>
              <h1 className="animate-fade-in-up font-display text-4xl font-bold tracking-tight text-balance delay-100 sm:text-5xl lg:text-6xl">
                The audit platform that{" "}
                <span className="bg-gradient-to-r from-sky-500 to-emerald-500 bg-clip-text text-transparent">
                  works the way you do
                </span>
              </h1>
              <p className="mx-auto mt-6 max-w-2xl animate-fade-in-up text-lg leading-relaxed text-muted-foreground text-balance delay-200">
                Ledgance brings engagement management, working papers, evidence
                versioning, and trial balance mapping into one modern,
                collaborative workspace — built for multi-tenant audit firms.
              </p>
              <div className="mt-8 flex animate-fade-in-up flex-col items-center justify-center gap-4 delay-300 sm:flex-row">
                <Link href="/signup">
                  <Button
                    size="lg"
                    className="h-12 px-8 text-base font-semibold"
                  >
                    Get started free
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
                <Link href="/login">
                  <Button
                    variant="outline"
                    size="lg"
                    className="h-12 px-8 text-base font-semibold"
                  >
                    Sign in
                  </Button>
                </Link>
              </div>
              <p className="mt-4 animate-fade-in-up text-sm text-muted-foreground delay-400">
                No credit card required · 14-day free trial · Cancel anytime
              </p>
            </div>

            {/* Hero preview mockup */}
            <div className="mt-16 animate-fade-in-up delay-500">
              <div className="overflow-hidden rounded-2xl border border-border/60 bg-card shadow-2xl shadow-primary/5">
                <div className="flex items-center gap-2 border-b border-border/60 bg-muted/30 px-4 py-3">
                  <div className="flex gap-1.5">
                    <div className="h-3 w-3 rounded-full bg-red-400" />
                    <div className="h-3 w-3 rounded-full bg-amber-400" />
                    <div className="h-3 w-3 rounded-full bg-emerald-400" />
                  </div>
                  <div className="ml-4 flex items-center gap-2 text-xs text-muted-foreground">
                    <Lock className="h-3 w-3" />
                    app.ledgance.io/dashboard
                  </div>
                </div>
                <div className="grid grid-cols-12 gap-0">
                  {/* Sidebar mock */}
                  <div className="col-span-3 hidden border-r border-border/60 p-4 lg:block">
                    <div className="mb-4 flex items-center gap-2">
                      <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary">
                        <ShieldCheck className="h-4 w-4 text-primary-foreground" />
                      </div>
                      <div className="h-3 w-24 rounded bg-muted" />
                    </div>
                    <div className="space-y-2">
                      {[
                        "Dashboard",
                        "Clients",
                        "Engagements",
                        "Documents",
                        "Working Papers",
                        "Trial Balance",
                      ].map((item, i) => (
                        <div
                          key={item}
                          className={`flex items-center gap-2 rounded-lg px-3 py-2 text-xs ${
                            i === 0
                              ? "bg-primary/10 font-medium text-primary"
                              : "text-muted-foreground"
                          }`}
                        >
                          <div className="h-3.5 w-3.5 rounded bg-current opacity-60" />
                          {item}
                        </div>
                      ))}
                    </div>
                  </div>
                  {/* Main content mock */}
                  <div className="col-span-12 p-6 lg:col-span-9">
                    <div className="mb-6 flex items-center justify-between">
                      <div>
                        <div className="h-5 w-48 rounded bg-foreground/80" />
                        <div className="mt-2 h-3 w-32 rounded bg-muted" />
                      </div>
                      <div className="h-9 w-28 rounded-lg bg-primary" />
                    </div>
                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                      {[
                        {
                          label: "Active Engagements",
                          value: "6",
                          icon: BarChart3,
                          color: "text-sky-500",
                        },
                        {
                          label: "Papers for Review",
                          value: "12",
                          icon: ClipboardCheck,
                          color: "text-amber-500",
                        },
                        {
                          label: "Budget Utilization",
                          value: "78%",
                          icon: Layers,
                          color: "text-emerald-500",
                        },
                      ].map((stat) => (
                        <div
                          key={stat.label}
                          className="rounded-xl border border-border/60 bg-background p-4"
                        >
                          <stat.icon className={`h-5 w-5 ${stat.color}`} />
                          <div className="mt-3 text-2xl font-bold">
                            {stat.value}
                          </div>
                          <div className="mt-1 text-xs text-muted-foreground">
                            {stat.label}
                          </div>
                        </div>
                      ))}
                    </div>
                    <div className="mt-4 rounded-xl border border-border/60 bg-background p-4">
                      <div className="mb-3 flex items-center justify-between">
                        <div className="h-3.5 w-24 rounded bg-foreground/60" />
                        <div className="h-3.5 w-16 rounded bg-muted" />
                      </div>
                      <div className="space-y-2">
                        {[1, 2, 3, 4].map((i) => (
                          <div
                            key={i}
                            className="flex items-center gap-3 rounded-lg border border-border/40 p-3"
                          >
                            <div className="h-8 w-8 rounded-full bg-gradient-to-br from-sky-400 to-emerald-400" />
                            <div className="flex-1">
                              <div className="h-3 w-32 rounded bg-foreground/40" />
                              <div className="mt-1.5 h-2.5 w-48 rounded bg-muted" />
                            </div>
                            <div className="h-6 w-16 rounded-full bg-emerald-100 dark:bg-emerald-950" />
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Stats bar */}
        <section className="border-y border-border/60 bg-muted/20">
          <div className="mx-auto max-w-7xl px-6 py-12">
            <div className="grid grid-cols-2 gap-8 lg:grid-cols-4">
              {stats.map((stat) => (
                <div key={stat.label} className="text-center">
                  <div className="font-display text-3xl font-bold text-primary lg:text-4xl">
                    {stat.value}
                  </div>
                  <div className="mt-1 text-sm text-muted-foreground">
                    {stat.label}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Features */}
        <section id="features" className="scroll-mt-20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Platform
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                Everything your audit team needs
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Four core modules that cover the entire audit lifecycle — from
                engagement setup to final sign-off.
              </p>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-2">
              {features.map((feature) => (
                <div
                  key={feature.title}
                  className="group relative overflow-hidden rounded-2xl border border-border/60 bg-card p-8 transition-all hover:border-primary/30 hover:shadow-lg hover:shadow-primary/5"
                >
                  <div
                    className={`mb-5 flex h-12 w-12 items-center justify-center rounded-xl ${feature.bg}`}
                  >
                    <feature.icon className={`h-6 w-6 ${feature.color}`} />
                  </div>
                  <h3 className="font-display text-xl font-semibold">
                    {feature.title}
                  </h3>
                  <p className="mt-3 leading-relaxed text-muted-foreground">
                    {feature.description}
                  </p>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* How it works */}
        <section id="how-it-works" className="scroll-mt-20 bg-muted/20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Workflow
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                From planning to sign-off in five steps
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                A structured workflow that mirrors how auditors actually work —
                not a generic project management tool retrofitted for audit.
              </p>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-3 lg:grid-cols-5">
              {workflow.map((step, i) => (
                <div
                  key={step.step}
                  className="relative rounded-2xl border border-border/60 bg-card p-6"
                >
                  <div className="flex items-center justify-between">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10">
                      <step.icon className="h-5 w-5 text-primary" />
                    </div>
                    <span className="font-display text-2xl font-bold text-muted-foreground/30">
                      {step.step}
                    </span>
                  </div>
                  <h3 className="mt-4 font-semibold">{step.title}</h3>
                  <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
                    {step.description}
                  </p>
                  {i < workflow.length - 1 && (
                    <ArrowRight className="absolute -right-3 top-1/2 hidden h-5 w-5 -translate-y-1/2 text-border lg:block" />
                  )}
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Security */}
        <section id="security" className="scroll-mt-20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="grid items-center gap-12 lg:grid-cols-2">
              <div>
                <Badge variant="secondary" className="mb-4">
                  Security & Compliance
                </Badge>
                <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                  Built for the trust that audit demands
                </h2>
                <p className="mt-4 text-lg leading-relaxed text-muted-foreground">
                  Every document, sign-off, and review note is captured in an
                  immutable audit trail. Data is encrypted at rest and in
                  transit, with per-tenant isolation and role-based access
                  controls.
                </p>
                <div className="mt-8 space-y-4">
                  {[
                    {
                      icon: Lock,
                      title: "End-to-end encryption",
                      desc: "AES-256 at rest, TLS 1.3 in transit",
                    },
                    {
                      icon: Users,
                      title: "Multi-tenant isolation",
                      desc: "Per-organization data separation with RLS",
                    },
                    {
                      icon: ShieldCheck,
                      title: "SOC 2 Type II",
                      desc: "Independently audited and certified",
                    },
                    {
                      icon: GitBranch,
                      title: "Immutable audit trail",
                      desc: "Every action logged and traceable",
                    },
                  ].map((item) => (
                    <div key={item.title} className="flex items-start gap-4">
                      <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-primary/10">
                        <item.icon className="h-5 w-5 text-primary" />
                      </div>
                      <div>
                        <h4 className="font-semibold">{item.title}</h4>
                        <p className="text-sm text-muted-foreground">
                          {item.desc}
                        </p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
              <div className="relative">
                <div className="absolute inset-0 rounded-3xl bg-gradient-to-br from-sky-500/10 to-emerald-500/10 blur-2xl" />
                <div className="relative overflow-hidden rounded-3xl border border-border/60 bg-card p-8">
                  <div className="flex items-center gap-3 border-b border-border/60 pb-6">
                    <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-sky-500 to-emerald-500">
                      <ShieldCheck className="h-6 w-6 text-white" />
                    </div>
                    <div>
                      <div className="font-display font-bold">
                        SOC 2 Type II
                      </div>
                      <div className="text-sm text-muted-foreground">
                        Certified & Compliant
                      </div>
                    </div>
                  </div>
                  <div className="mt-6 space-y-3">
                    {[
                      "Security",
                      "Availability",
                      "Processing Integrity",
                      "Confidentiality",
                      "Privacy",
                    ].map((cert) => (
                      <div
                        key={cert}
                        className="flex items-center justify-between rounded-lg border border-border/60 bg-background px-4 py-3"
                      >
                        <span className="text-sm font-medium">{cert}</span>
                        <CheckCircle2 className="h-5 w-5 text-emerald-500" />
                      </div>
                    ))}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Testimonials */}
        <section className="bg-muted/20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Trusted by auditors
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                What audit teams say
              </h2>
            </div>
            <div className="mt-16 grid gap-6 md:grid-cols-3">
              {testimonials.map((t) => (
                <div
                  key={t.author}
                  className="flex flex-col rounded-2xl border border-border/60 bg-card p-8"
                >
                  <div className="flex gap-0.5">
                    {Array.from({ length: 5 }).map((_, i) => (
                      <Star
                        key={i}
                        className="h-4 w-4 fill-amber-400 text-amber-400"
                      />
                    ))}
                  </div>
                  <p className="mt-4 flex-1 leading-relaxed text-foreground/90">
                    &ldquo;{t.quote}&rdquo;
                  </p>
                  <div className="mt-6 flex items-center gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-full bg-gradient-to-br from-sky-400 to-emerald-400 text-sm font-semibold text-white">
                      {t.initials}
                    </div>
                    <div>
                      <div className="text-sm font-semibold">{t.author}</div>
                      <div className="text-xs text-muted-foreground">
                        {t.role}
                      </div>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* Pricing */}
        <section id="pricing" className="scroll-mt-20">
          <div className="mx-auto max-w-7xl px-6 py-20 lg:py-28">
            <div className="mx-auto max-w-2xl text-center">
              <Badge variant="secondary" className="mb-4">
                Pricing
              </Badge>
              <h2 className="font-display text-3xl font-bold tracking-tight text-balance sm:text-4xl">
                Simple, transparent pricing
              </h2>
              <p className="mt-4 text-lg text-muted-foreground text-balance">
                Per-user pricing that scales with your firm. No hidden fees, no
                surprises.
              </p>
            </div>
            <div className="mt-16 grid gap-6 lg:grid-cols-3">
              {pricingPlans.map((plan) => (
                <div
                  key={plan.name}
                  className={`relative flex flex-col rounded-2xl border p-8 ${
                    plan.highlighted
                      ? "border-primary bg-primary/5 shadow-xl shadow-primary/10"
                      : "border-border/60 bg-card"
                  }`}
                >
                  {plan.highlighted && (
                    <div className="absolute -top-3 left-1/2 -translate-x-1/2">
                      <Badge className="px-3 py-1 text-xs font-semibold">
                        Most popular
                      </Badge>
                    </div>
                  )}
                  <h3 className="font-display text-xl font-bold">
                    {plan.name}
                  </h3>
                  <p className="mt-2 text-sm text-muted-foreground">
                    {plan.description}
                  </p>
                  <div className="mt-6 flex items-baseline gap-1">
                    <span className="font-display text-4xl font-bold">
                      {plan.price}
                    </span>
                    <span className="text-sm text-muted-foreground">
                      {plan.period}
                    </span>
                  </div>
                  <div className="mt-6 space-y-3">
                    {plan.features.map((feat) => (
                      <div key={feat} className="flex items-center gap-3">
                        <CheckCircle2 className="h-4.5 w-4.5 flex-shrink-0 text-emerald-500" />
                        <span className="text-sm">{feat}</span>
                      </div>
                    ))}
                  </div>
                  <div className="mt-8 flex-1" />
                  <Link href="/signup">
                    <Button
                      className="w-full"
                      variant={plan.highlighted ? "default" : "outline"}
                    >
                      {plan.cta}
                    </Button>
                  </Link>
                </div>
              ))}
            </div>
          </div>
        </section>

        {/* CTA */}
        <section className="relative overflow-hidden">
          <div className="absolute inset-0 bg-gradient-to-br from-sky-500 to-emerald-500" />
          <div className="absolute inset-0 bg-grid opacity-10" />
          <div className="relative mx-auto max-w-4xl px-6 py-20 text-center lg:py-28">
            <h2 className="font-display text-3xl font-bold tracking-tight text-white text-balance sm:text-4xl lg:text-5xl">
              Ready to modernize your audit practice?
            </h2>
            <p className="mx-auto mt-4 max-w-2xl text-lg text-white/80 text-balance">
              Join the firms that have moved their entire audit workflow to
              Ledgance. Start your free trial today.
            </p>
            <div className="mt-8 flex flex-col items-center justify-center gap-4 sm:flex-row">
              <Link href="/signup">
                <Button
                  size="lg"
                  className="h-12 bg-white px-8 text-base font-semibold text-primary hover:bg-white/90"
                >
                  Get started free
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Button>
              </Link>
              <Link href="/login">
                <Button
                  size="lg"
                  variant="outline"
                  className="h-12 border-white/30 bg-white/10 px-8 text-base font-semibold text-white hover:bg-white/20 hover:text-white"
                >
                  Sign in
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
