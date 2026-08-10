"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowRight,
  CheckCircle2,
  CreditCard,
  Loader2,
  Lock,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { useAuth } from "@/components/auth-context";
import { useApiMutation } from "@/hooks/query";
import { useSession } from "@/hooks/session";
import {
  planFeatures,
  planPresentation,
  platformOf,
  usePlans,
} from "@/lib/plans";

/**
 * The Stripe seam: this page summarizes the chosen plan and hands off to checkout. The
 * checkout endpoint arrives with the Stripe integration; until the API answers, the
 * attempt surfaces as a graceful error and the Free plan remains fully usable.
 */
export function SubscribeView({ planCode }: { planCode: string }) {
  const router = useRouter();
  const { user, loading: authLoading } = useAuth();
  const { data: session } = useSession(!!user);
  const { data: plans, isLoading: plansLoading } = usePlans();

  useEffect(() => {
    if (!authLoading && !user) {
      router.replace(`/login`);
    }
  }, [authLoading, user, router]);

  useEffect(() => {
    if (session?.needsOnboarding) {
      router.replace(`/onboarding?plan=${planCode}`);
    }
  }, [session, planCode, router]);

  const checkout = useApiMutation<{ checkoutUrl: string }, { planCode: string }>(
    "/api/billing/checkout",
    "post",
    {
      onSuccess: (result) => {
        window.location.assign(result.checkoutUrl);
      },
    },
  );

  const plan = plans?.find((row) => row.code === planCode);
  const presentation = planPresentation[planCode];
  const platform = platformOf(planCode);
  const hasDefinedPrice = !!presentation?.price.period;

  if (authLoading || plansLoading) {
    return (
      <div className="mx-auto w-full max-w-lg space-y-4 p-6">
        <Skeleton className="h-8 w-56" />
        <Skeleton className="h-64 w-full rounded-2xl" />
        <Skeleton className="h-11 w-full" />
      </div>
    );
  }

  if (!plan || !presentation) {
    return (
      <div className="mx-auto w-full max-w-lg p-6 text-center">
        <AlertCircle className="mx-auto h-8 w-8 text-destructive" />
        <h1 className="mt-4 font-display text-xl font-bold">
          That plan does not exist
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Pick a plan from the pricing page to continue.
        </p>
        <Link href="/pricing">
          <Button className="mt-6">View plans</Button>
        </Link>
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-lg p-6">
      <div className="text-center">
        <Badge variant="secondary" className="mb-3">
          {platform === "accounting" ? "Ledgance Accounting" : "Ledgance Audit"}
        </Badge>
        <h1 className="font-display text-2xl font-bold tracking-tight">
          Confirm your plan
        </h1>
        {session?.organizationName && (
          <p className="mt-2 text-sm text-muted-foreground">
            Subscribing for{" "}
            <span className="font-medium text-foreground">
              {session.organizationName}
            </span>
          </p>
        )}
      </div>

      <div className="mt-8 rounded-2xl border border-border/60 bg-card p-6">
        <div className="flex items-baseline justify-between">
          <h2 className="font-display text-xl font-bold">
            {presentation.name}
          </h2>
          <div className="flex items-baseline gap-1">
            <span className="font-display text-2xl font-bold">
              {presentation.price.label}
            </span>
            {presentation.price.period && (
              <span className="text-sm text-muted-foreground">
                {presentation.price.period}
              </span>
            )}
          </div>
        </div>
        <p className="mt-1 text-sm text-muted-foreground">
          {presentation.tagline}
        </p>
        <ul className="mt-5 space-y-2.5 border-t border-border/60 pt-5">
          {planFeatures(plan, platform).map((feature) => (
            <li key={feature} className="flex items-start gap-2.5">
              <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0 text-emerald-500" />
              <span className="text-sm">{feature}</span>
            </li>
          ))}
        </ul>
      </div>

      {checkout.isError && (
        <div className="mt-6 rounded-xl border border-amber-500/40 bg-amber-50 p-4 dark:bg-amber-950/20">
          <div className="flex items-start gap-3">
            <AlertCircle className="mt-0.5 h-5 w-5 flex-shrink-0 text-amber-600" />
            <div>
              <p className="text-sm font-medium">
                Online checkout is not available yet
              </p>
              <p className="mt-1 text-sm text-muted-foreground">
                Payments are launching shortly. Your account is ready on the
                Free plan in the meantime — everything you set up carries over
                when you upgrade.
              </p>
            </div>
          </div>
        </div>
      )}

      <div className="mt-6 space-y-3">
        {hasDefinedPrice ? (
          <Button
            className="h-11 w-full font-semibold"
            onClick={() => checkout.mutate({ planCode })}
            disabled={checkout.isPending}
          >
            {checkout.isPending ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Preparing secure checkout...
              </>
            ) : (
              <>
                <CreditCard className="mr-2 h-4 w-4" />
                Continue to secure checkout
              </>
            )}
          </Button>
        ) : (
          <div className="rounded-xl border border-border/60 bg-muted/30 p-4 text-center text-sm text-muted-foreground">
            Pricing for the {presentation.name} plan is announced at launch.
            Start on the Free plan today — you can upgrade the moment it opens.
          </div>
        )}
        <Link href="/dashboard" className="block">
          <Button variant="outline" className="h-11 w-full font-semibold">
            Continue on Free for now
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </Link>
      </div>

      <p className="mt-6 flex items-center justify-center gap-1.5 text-center text-xs text-muted-foreground">
        <Lock className="h-3 w-3" />
        Payments are processed securely by Stripe. Access is granted only after
        the subscription is confirmed server-side.
      </p>
    </div>
  );
}
