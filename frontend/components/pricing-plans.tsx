"use client";

import Link from "next/link";
import { AlertCircle, CheckCircle2, RefreshCw } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  planFeatures,
  planPresentation,
  plansForPlatform,
  priceLabel,
  usePlans,
  type Platform,
  type SubscriptionPlanRow,
} from "@/lib/plans";

function PlanCard({
  plan,
  platform,
}: {
  plan: SubscriptionPlanRow;
  platform: Platform;
}) {
  const presentation = planPresentation[plan.code];
  if (!presentation) return null;

  const price = priceLabel(plan);

  const signupHref = plan.isFree
    ? `/signup?platform=${platform}`
    : `/signup?platform=${platform}&plan=${plan.code}`;

  return (
    <div
      className={`relative flex flex-col rounded-2xl border p-8 ${
        presentation.highlighted
          ? "border-primary bg-primary/5 shadow-xl shadow-primary/10"
          : "border-border/60 bg-card"
      }`}
    >
      {presentation.highlighted && (
        <div className="absolute -top-3 left-1/2 -translate-x-1/2">
          <Badge className="px-3 py-1 text-xs font-semibold">Most popular</Badge>
        </div>
      )}
      <h3 className="font-display text-xl font-bold">{presentation.name}</h3>
      <p className="mt-2 min-h-10 text-sm text-muted-foreground">
        {presentation.tagline}
      </p>
      <div className="mt-6 flex items-baseline gap-1">
        <span
          className={`font-display font-bold ${
            price.period ? "text-4xl" : "text-2xl"
          }`}
        >
          {price.label}
        </span>
        {price.period && (
          <span className="text-sm text-muted-foreground">{price.period}</span>
        )}
      </div>
      <ul className="mt-6 space-y-3">
        {planFeatures(plan, platform).map((feature) => (
          <li key={feature} className="flex items-start gap-3">
            <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0 text-emerald-500" />
            <span className="text-sm">{feature}</span>
          </li>
        ))}
      </ul>
      <div className="mt-8 flex-1" />
      {plan.requiresContactSales ? (
        <a href="mailto:sales@ledgance.io?subject=Ledgance%20Enterprise">
          <Button className="w-full" variant="outline">
            Contact sales
          </Button>
        </a>
      ) : (
        <Link href={signupHref}>
          <Button
            className="w-full"
            variant={presentation.highlighted ? "default" : "outline"}
          >
            {plan.isFree ? "Start free" : `Choose ${presentation.name}`}
          </Button>
        </Link>
      )}
    </div>
  );
}

export function PricingPlans({ platform }: { platform: Platform }) {
  const { data: plans, isLoading, isError, refetch } = usePlans();

  if (isLoading) {
    return (
      <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
        {[1, 2, 3].map((i) => (
          <div key={i} className="rounded-2xl border border-border/60 bg-card p-8">
            <Skeleton className="h-6 w-24" />
            <Skeleton className="mt-3 h-4 w-full" />
            <Skeleton className="mt-6 h-10 w-32" />
            <div className="mt-6 space-y-3">
              {[1, 2, 3, 4, 5].map((line) => (
                <Skeleton key={line} className="h-4 w-full" />
              ))}
            </div>
            <Skeleton className="mt-8 h-10 w-full" />
          </div>
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="mx-auto max-w-md rounded-2xl border border-border/60 bg-card p-8 text-center">
        <AlertCircle className="mx-auto h-8 w-8 text-destructive" />
        <h3 className="mt-4 font-display text-lg font-semibold">
          Plans are unavailable right now
        </h3>
        <p className="mt-2 text-sm text-muted-foreground">
          We could not load the plan catalog. Please try again.
        </p>
        <Button className="mt-6" variant="outline" onClick={() => refetch()}>
          <RefreshCw className="mr-2 h-4 w-4" />
          Retry
        </Button>
      </div>
    );
  }

  const rows = plansForPlatform(plans, platform);

  return (
    <div className="grid gap-6 md:grid-cols-2 xl:grid-cols-3">
      {rows.map((plan) => (
        <PlanCard key={plan.code} plan={plan} platform={platform} />
      ))}
    </div>
  );
}
