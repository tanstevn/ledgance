"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowRight,
  Calculator,
  CheckCircle2,
  Loader2,
  ShieldCheck,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import { OrDivider, SocialAuthButtons } from "@/components/auth/social-buttons";
import { planPresentation, type Platform } from "@/lib/plans";

const panels: Record<
  Platform | "default",
  {
    gradient: string;
    icon: typeof ShieldCheck;
    headline: string;
    bullets: string[];
  }
> = {
  accounting: {
    gradient: "from-emerald-600 via-teal-700 to-emerald-800",
    icon: Calculator,
    headline: "Real double-entry books, minutes from now.",
    bullets: [
      "Free plan with a full entity and 300 transactions per period",
      "Balanced journal entries, periods and reconciliation",
      "Live trial balance and financial statements",
      "AI assistance included on every plan",
    ],
  },
  audit: {
    gradient: "from-sky-600 via-sky-700 to-indigo-800",
    icon: ShieldCheck,
    headline: "Your first engagement file, minutes from now.",
    bullets: [
      "Free plan with a client and two engagements",
      "Working papers with structured sign-offs",
      "Versioned evidence and an append-only trail",
      "AI assistance included on every plan",
    ],
  },
  default: {
    gradient: "from-emerald-600 via-teal-700 to-sky-800",
    icon: ShieldCheck,
    headline: "Two professional platforms. Start with one.",
    bullets: [
      "Ledgance Accounting: real double-entry bookkeeping",
      "Ledgance Audit: the complete audit lifecycle",
      "Each stands alone — connect them only if you want to",
      "Genuinely useful free plans on both sides",
    ],
  },
};

export function SignupForm({
  platform,
  plan,
}: {
  platform: Platform | null;
  plan: string | null;
}) {
  const router = useRouter();
  const { signUp } = useAuth();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);

  const panel = panels[platform ?? "default"];
  const PanelIcon = panel.icon;
  const planName = plan ? planPresentation[plan]?.name : null;

  const nextQuery = new URLSearchParams();
  if (platform) nextQuery.set("platform", platform);
  if (plan) nextQuery.set("plan", plan);
  const nextSuffix = nextQuery.size > 0 ? `?${nextQuery.toString()}` : "";

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name || !email || !password) {
      toast.error("Please fill in all fields.");
      return;
    }
    if (password.length < 6) {
      toast.error("Password must be at least 6 characters.");
      return;
    }
    setLoading(true);
    try {
      await signUp(name, email, password);
      toast.success("Account created! Welcome to Ledgance.");
      router.push(`/onboarding${nextSuffix}`);
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : "Something went wrong. Please try again.",
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen flex-col lg:flex-row">
      {/* Left panel - form */}
      <div className="flex flex-1 items-center justify-center p-6 lg:p-12">
        <div className="w-full max-w-sm">
          <div className="mb-8 text-center lg:text-left">
            <Link
              href="/"
              className="mb-6 inline-flex items-center gap-2.5 lg:hidden"
            >
              <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-primary">
                <ShieldCheck
                  className="h-5 w-5 text-primary-foreground"
                  strokeWidth={2.5}
                />
              </div>
              <span className="font-display text-lg font-bold tracking-tight">
                Ledgance
              </span>
            </Link>
            <h1 className="font-display text-2xl font-bold tracking-tight">
              Create your account
            </h1>
            {platform && (
              <p className="mt-2 text-sm text-muted-foreground">
                Starting with{" "}
                <span className="font-medium text-foreground">
                  Ledgance {platform === "accounting" ? "Accounting" : "Audit"}
                </span>
                {planName && planName !== "Free" ? (
                  <>
                    {" "}
                    on the{" "}
                    <span className="font-medium text-foreground">
                      {planName}
                    </span>{" "}
                    plan
                  </>
                ) : (
                  " on the Free plan"
                )}
                .
              </p>
            )}
            <p className="mt-2 text-sm text-muted-foreground">
              Already have an account?{" "}
              <Link
                href="/login"
                className="font-medium text-primary hover:underline"
              >
                Sign in
              </Link>
            </p>
          </div>

          <div className="mb-5 space-y-5">
            <SocialAuthButtons
              redirectTo={`/onboarding${nextSuffix}`}
              action="Sign up"
            />
            <OrDivider />
          </div>

          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="name">Full name</Label>
              <Input
                id="name"
                type="text"
                placeholder="Jordan Avery"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoComplete="name"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="email">Work email</Label>
              <Input
                id="email"
                type="email"
                placeholder="you@firm.com"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="email"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="password">Password</Label>
              <Input
                id="password"
                type="password"
                placeholder="At least 6 characters"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="new-password"
              />
            </div>
            <Button type="submit" className="w-full" disabled={loading}>
              {loading ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Creating account...
                </>
              ) : (
                <>
                  Create account
                  <ArrowRight className="ml-2 h-4 w-4" />
                </>
              )}
            </Button>
          </form>

          <p className="mt-6 text-center text-xs text-muted-foreground">
            By signing up, you agree to our{" "}
            <Link href="#" className="underline hover:text-foreground">
              Terms
            </Link>{" "}
            and{" "}
            <Link href="#" className="underline hover:text-foreground">
              Privacy Policy
            </Link>
            .
          </p>
        </div>
      </div>

      {/* Right panel */}
      <div
        className={`relative hidden flex-1 overflow-hidden bg-gradient-to-br lg:block ${panel.gradient}`}
      >
        <div className="pointer-events-none absolute inset-0 bg-grid opacity-10" />
        <div className="pointer-events-none absolute -left-20 top-20 h-96 w-96 rounded-full bg-white/10 blur-3xl" />
        <div className="pointer-events-none absolute bottom-0 right-0 h-96 w-96 rounded-full bg-white/10 blur-3xl" />
        <div className="relative flex h-full flex-col justify-between p-12 text-white">
          <Link href="/" className="ml-auto flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-white/20 backdrop-blur">
              <PanelIcon className="h-5 w-5 text-white" strokeWidth={2.5} />
            </div>
            <span className="font-display text-lg font-bold tracking-tight">
              Ledgance
            </span>
          </Link>
          <div className="max-w-md">
            <h2 className="font-display text-3xl font-bold leading-tight">
              {panel.headline}
            </h2>
            <div className="mt-8 space-y-3">
              {panel.bullets.map((item) => (
                <div
                  key={item}
                  className="flex items-center gap-3 text-white/90"
                >
                  <CheckCircle2 className="h-5 w-5 flex-shrink-0 text-white" />
                  <span className="text-sm">{item}</span>
                </div>
              ))}
            </div>
          </div>
          <p className="text-sm text-white/60">
            No credit card required for the free plan.
          </p>
        </div>
      </div>
    </div>
  );
}
