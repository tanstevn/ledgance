"use client";

import { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  ArrowRight,
  Calculator,
  CheckCircle2,
  Loader2,
  MailCheck,
  ShieldCheck,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import { OrDivider, SocialAuthButtons } from "@/components/auth/social-buttons";

export default function LoginPage() {
  const router = useRouter();
  const { signIn, resetPassword } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [mode, setMode] = useState<"login" | "reset" | "reset-sent">("login");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email || !password) {
      toast.error("Please enter your email and password.");
      return;
    }
    setLoading(true);
    try {
      await signIn(email, password);
      router.push("/dashboard");
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : "Something went wrong. Please try again.",
      );
    } finally {
      toast.success("Welcome back!");
      // setLoading(false);
    }
  };

  const handleReset = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email) {
      toast.error("Enter the email you signed up with.");
      return;
    }
    setLoading(true);
    try {
      await resetPassword(email);
      setMode("reset-sent");
    } catch (error) {
      toast.error(
        error instanceof Error
          ? error.message
          : "Could not send the reset email. Please try again.",
      );
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen flex-col lg:flex-row">
      {/* Form panel */}
      <div className="flex flex-1 items-center justify-center p-6 lg:p-12">
        <div className="w-full max-w-sm">
          <Link href="/" className="mb-10 inline-flex items-center gap-2.5">
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

          {mode === "login" && (
            <>
              <h1 className="font-display text-2xl font-bold tracking-tight">
                Welcome back
              </h1>
              <p className="mt-2 text-sm text-muted-foreground">
                New to Ledgance?{" "}
                <Link
                  href="/signup"
                  className="font-medium text-primary hover:underline"
                >
                  Create an account
                </Link>
              </p>

              <div className="mt-8 space-y-5">
                <SocialAuthButtons redirectTo="/dashboard" />
                <OrDivider />
                <form onSubmit={handleSubmit} className="space-y-4">
                  <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
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
                    <div className="flex items-center justify-between">
                      <Label htmlFor="password">Password</Label>
                      <button
                        type="button"
                        onClick={() => setMode("reset")}
                        className="text-xs font-medium text-primary hover:underline"
                      >
                        Forgot password?
                      </button>
                    </div>
                    <Input
                      id="password"
                      type="password"
                      placeholder="Your password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      autoComplete="current-password"
                    />
                  </div>
                  <Button
                    type="submit"
                    className="h-11 w-full font-semibold"
                    disabled={loading}
                  >
                    {loading ? (
                      <>
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                        Signing in...
                      </>
                    ) : (
                      <>
                        Sign in
                        <ArrowRight className="ml-2 h-4 w-4" />
                      </>
                    )}
                  </Button>
                </form>
              </div>
            </>
          )}

          {mode === "reset" && (
            <>
              <button
                type="button"
                onClick={() => setMode("login")}
                className="mb-4 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
              >
                <ArrowLeft className="h-4 w-4" />
                Back to sign in
              </button>
              <h1 className="font-display text-2xl font-bold tracking-tight">
                Reset your password
              </h1>
              <p className="mt-2 text-sm text-muted-foreground">
                Enter your email and we&apos;ll send you a link to set a new
                password.
              </p>
              <form onSubmit={handleReset} className="mt-8 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="reset-email">Email</Label>
                  <Input
                    id="reset-email"
                    type="email"
                    placeholder="you@firm.com"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    autoComplete="email"
                    autoFocus
                  />
                </div>
                <Button
                  type="submit"
                  className="h-11 w-full font-semibold"
                  disabled={loading}
                >
                  {loading ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : null}
                  Send reset link
                </Button>
              </form>
            </>
          )}

          {mode === "reset-sent" && (
            <div className="text-center lg:text-left">
              <div className="mb-5 inline-flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-100 dark:bg-emerald-950/40">
                <MailCheck className="h-6 w-6 text-emerald-500" />
              </div>
              <h1 className="font-display text-2xl font-bold tracking-tight">
                Check your inbox
              </h1>
              <p className="mt-2 text-sm text-muted-foreground">
                If an account exists for{" "}
                <span className="font-medium text-foreground">{email}</span>, a
                password reset link is on its way.
              </p>
              <Button
                variant="outline"
                className="mt-6 font-semibold"
                onClick={() => setMode("login")}
              >
                <ArrowLeft className="mr-2 h-4 w-4" />
                Back to sign in
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* Brand panel */}
      <div className="relative hidden flex-1 overflow-hidden bg-gradient-to-br from-sky-700 via-sky-800 to-emerald-800 lg:block">
        <div className="pointer-events-none absolute inset-0 bg-grid opacity-10" />
        <div className="pointer-events-none absolute -right-20 top-20 h-96 w-96 rounded-full bg-white/10 blur-3xl" />
        <div className="pointer-events-none absolute bottom-0 left-0 h-96 w-96 rounded-full bg-emerald-300/10 blur-3xl" />
        <div className="relative flex h-full flex-col justify-between p-12 text-white">
          <div />
          <div className="max-w-md">
            <div className="flex items-center gap-3">
              <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/15 backdrop-blur">
                <Calculator className="h-5.5 w-5.5 text-emerald-300" />
              </div>
              <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-white/15 backdrop-blur">
                <ShieldCheck className="h-5.5 w-5.5 text-sky-300" />
              </div>
            </div>
            <h2 className="mt-6 font-display text-3xl font-bold leading-tight">
              Your books and your audits, exactly where you left them.
            </h2>
            <div className="mt-8 space-y-3">
              {[
                "Real double-entry books with live statements",
                "Engagement files that are always review-ready",
                "AI assistance that proposes — you decide",
                "One organization, your choice of platforms",
              ].map((item) => (
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
            Professional accounting and audit, done right.
          </p>
        </div>
      </div>
    </div>
  );
}
