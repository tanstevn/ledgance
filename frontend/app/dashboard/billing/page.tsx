"use client";

import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import {
  ArrowUpRight,
  Calculator,
  CheckCircle2,
  Loader2,
  Plus,
  ShieldCheck,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";
import { CrossSellCard } from "@/components/cross-sell";
import { useAuth } from "@/components/auth-context";
import { useApiMutation } from "@/hooks/query";
import { enabledPlatforms, useSession } from "@/hooks/session";
import {
  planFeatures,
  planPresentation,
  usePlans,
  type Platform,
} from "@/lib/plans";

function PlanPanel({ platform }: { platform: Platform }) {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const { data: plans, isLoading } = usePlans();

  const moduleName = platform === "accounting" ? "Accounting" : "Audit";
  const planCode =
    session?.plans.find((plan) => plan.module === moduleName)?.plan ?? "Free";
  const plan = plans?.find((row) => row.code === planCode);
  const presentation = planPresentation[planCode];

  const Icon = platform === "accounting" ? Calculator : ShieldCheck;
  const accent =
    platform === "accounting" ? "text-emerald-500" : "text-sky-500";

  return (
    <Card className="border-border/60">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <CardTitle className="flex items-center gap-2.5 font-display text-base font-semibold">
          <Icon className={`h-5 w-5 ${accent}`} />
          Ledgance {moduleName}
        </CardTitle>
        <Badge variant={planCode === "Free" ? "secondary" : "default"}>
          {presentation?.name ?? planCode}
        </Badge>
      </CardHeader>
      <CardContent>
        {isLoading || !session ? (
          <div className="space-y-2">
            {[1, 2, 3, 4].map((i) => (
              <Skeleton key={i} className="h-4 w-full" />
            ))}
          </div>
        ) : plan ? (
          <ul className="space-y-2">
            {planFeatures(plan, platform)
              .slice(0, 6)
              .map((feature) => (
                <li key={feature} className="flex items-start gap-2.5">
                  <CheckCircle2 className="mt-0.5 h-4 w-4 flex-shrink-0 text-emerald-500" />
                  <span className="text-sm">{feature}</span>
                </li>
              ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">
            Plan details are unavailable right now.
          </p>
        )}
        <Link href={`/pricing?platform=${platform}`}>
          <Button variant="outline" size="sm" className="mt-5 font-semibold">
            {planCode === "Free" ? "Upgrade" : "Compare plans"}
            <ArrowUpRight className="ml-2 h-4 w-4" />
          </Button>
        </Link>
      </CardContent>
    </Card>
  );
}

/**
 * Shown for a platform the organization has not activated. Enabling is free and only
 * changes what the dashboard offers; it requires the organization owner.
 */
function ActivatePlatformCard({ platform }: { platform: Platform }) {
  const queryClient = useQueryClient();
  const moduleName = platform === "accounting" ? "Accounting" : "Audit";
  const Icon = platform === "accounting" ? Calculator : ShieldCheck;
  const accent =
    platform === "accounting" ? "text-emerald-500" : "text-sky-500";

  const enable = useApiMutation<boolean, { product: string }>(
    "/api/organization/products",
    "post",
    {
      onSuccess: async () => {
        toast.success(`Ledgance ${moduleName} is now available.`);
        await queryClient.invalidateQueries({ queryKey: ["session"] });
      },
      onError: (errors) => toast.error(errors.join(" ")),
    },
  );

  return (
    <Card className="border-dashed border-border">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <CardTitle className="flex items-center gap-2.5 font-display text-base font-semibold text-muted-foreground">
          <Icon className={`h-5 w-5 ${accent}`} />
          Ledgance {moduleName}
        </CardTitle>
        <Badge variant="outline">Not activated</Badge>
      </CardHeader>
      <CardContent>
        <p className="text-sm text-muted-foreground">
          {platform === "accounting"
            ? "Real double-entry bookkeeping: entities, journal, reconciliation and live financial statements."
            : "The complete audit lifecycle: clients, engagements, working papers, evidence and reports."}{" "}
          Your organization can add it any time — starting free — without
          affecting your current platform.
        </p>
        <div className="mt-5 flex flex-wrap items-center gap-3">
          <Button
            size="sm"
            className="font-semibold"
            onClick={() => enable.mutate({ product: moduleName })}
            disabled={enable.isPending}
          >
            {enable.isPending ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Activating...
              </>
            ) : (
              <>
                <Plus className="mr-2 h-4 w-4" />
                Activate free
              </>
            )}
          </Button>
          <Link href={`/${platform}`}>
            <Button size="sm" variant="ghost" className="text-muted-foreground">
              Learn more
            </Button>
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}

export default function BillingPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const platforms = enabledPlatforms(session);

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="font-display text-2xl font-bold tracking-tight">
          Plans & billing
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          {session?.organizationName
            ? `Subscriptions for ${session.organizationName}. Each platform is subscribed separately.`
            : "Each platform is subscribed separately — you never need both."}
        </p>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {platforms.includes("accounting") ? (
          <PlanPanel platform="accounting" />
        ) : (
          session && <ActivatePlatformCard platform="accounting" />
        )}
        {platforms.includes("audit") ? (
          <PlanPanel platform="audit" />
        ) : (
          session && <ActivatePlatformCard platform="audit" />
        )}
      </div>

      <CrossSellCard />

      <p className="text-xs text-muted-foreground">
        Plan limits shown here are read from the server and enforced by the
        server. Online checkout and payment management arrive with the Stripe
        integration.
      </p>
    </div>
  );
}
