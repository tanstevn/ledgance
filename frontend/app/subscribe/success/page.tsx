"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  ArrowRight,
  CheckCircle2,
  Clock,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { CrossSellCard } from "@/components/cross-sell";
import { useAuth } from "@/components/auth-context";
import { hasPaidPlan, planForPlatform, useSession } from "@/hooks/session";

/**
 * The post-checkout landing. Nothing here trusts the redirect: the page shows a
 * subscription as active only once /api/session reports the paid plan, and the optional
 * cross-platform recommendation appears only after that server-side confirmation.
 */
export default function SubscribeSuccessPage() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuth();
  const { data: session, isLoading, refetch, isRefetching } = useSession(!!user);

  useEffect(() => {
    if (!authLoading && !user) {
      router.replace("/login");
    }
  }, [authLoading, user, router]);

  const paidAccounting = hasPaidPlan(session, "accounting");
  const paidAudit = hasPaidPlan(session, "audit");
  const confirmedPlatform = paidAccounting
    ? "accounting"
    : paidAudit
      ? "audit"
      : null;

  return (
    <div className="flex min-h-screen flex-col">
      <header className="flex h-16 items-center border-b border-border/60 px-6">
        <Link href="/" className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-primary">
            <ShieldCheck
              className="h-4.5 w-4.5 text-primary-foreground"
              strokeWidth={2.5}
            />
          </div>
          <span className="font-display text-base font-bold tracking-tight">
            Ledgance
          </span>
        </Link>
      </header>
      <main className="flex flex-1 items-center justify-center py-10">
        <div className="mx-auto w-full max-w-lg space-y-6 p-6">
          {authLoading || isLoading ? (
            <>
              <Skeleton className="mx-auto h-14 w-14 rounded-full" />
              <Skeleton className="mx-auto h-7 w-64" />
              <Skeleton className="h-40 w-full rounded-2xl" />
            </>
          ) : confirmedPlatform ? (
            <>
              <div className="text-center">
                <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-100 dark:bg-emerald-950/40">
                  <CheckCircle2 className="h-7 w-7 text-emerald-500" />
                </div>
                <h1 className="mt-5 font-display text-2xl font-bold tracking-tight">
                  Your subscription is active
                </h1>
                <p className="mt-2 text-sm text-muted-foreground">
                  {session?.organizationName ?? "Your organization"} is on the{" "}
                  <span className="font-medium text-foreground">
                    {planForPlatform(session, confirmedPlatform)?.plan}
                  </span>{" "}
                  plan for Ledgance{" "}
                  {confirmedPlatform === "accounting" ? "Accounting" : "Audit"}.
                </p>
              </div>

              <CrossSellCard />

              <Link href="/dashboard" className="block">
                <Button className="h-11 w-full font-semibold">
                  Continue to{" "}
                  {confirmedPlatform === "accounting" ? "Accounting" : "Audit"}
                  <ArrowRight className="ml-2 h-4 w-4" />
                </Button>
              </Link>
            </>
          ) : (
            <div className="text-center">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-muted">
                <Clock className="h-7 w-7 text-muted-foreground" />
              </div>
              <h1 className="mt-5 font-display text-2xl font-bold tracking-tight">
                Waiting for confirmation
              </h1>
              <p className="mt-2 text-sm text-muted-foreground">
                We have not received the subscription confirmation yet. This
                usually takes a moment — access is granted only once the
                subscription is verified server-side.
              </p>
              <div className="mt-6 flex flex-col items-center gap-3 sm:flex-row sm:justify-center">
                <Button
                  variant="outline"
                  onClick={() => refetch()}
                  disabled={isRefetching}
                >
                  <RefreshCw
                    className={`mr-2 h-4 w-4 ${isRefetching ? "animate-spin" : ""}`}
                  />
                  Check again
                </Button>
                <Link href="/dashboard">
                  <Button variant="ghost">Go to dashboard</Button>
                </Link>
              </div>
            </div>
          )}
        </div>
      </main>
    </div>
  );
}
