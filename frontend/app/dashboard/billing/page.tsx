"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import {
  ArrowUpRight,
  Calculator,
  CalendarClock,
  CheckCircle2,
  ChevronLeft,
  ChevronRight,
  CreditCard,
  Loader2,
  Plus,
  ShieldCheck,
  TriangleAlert,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";
import { CrossSellCard } from "@/components/cross-sell";
import { useAuth } from "@/components/auth-context";
import { useApiAction, useApiMutation } from "@/hooks/query";
import { enabledPlatforms, useSession } from "@/hooks/session";
import {
  billingStateFor,
  fmtRenewal,
  moduleOf,
  statusLabel,
  useBillingOverview,
  type BillingProductState,
} from "@/lib/billing";
import {
  planFeatures,
  planPresentation,
  plansForPlatform,
  priceLabel,
  usePlans,
  type Platform,
  type SubscriptionPlanRow,
} from "@/lib/plans";

const statusTone = (state: BillingProductState) => {
  if (state.status === "PastDue") return "bg-destructive text-destructive-foreground";
  if (state.cancelAtPeriodEnd) return "bg-warning text-warning-foreground";
  if (state.plan === "Free") return "bg-muted text-muted-foreground";
  return "bg-success text-success-foreground";
};

/**
 * Lays the plans out side by side and pages through them with edge controls, so comparing
 * plans is a horizontal scan rather than a vertical scroll. The arrows appear only when there
 * is something further in that direction; the row also scrolls by swipe, wheel and keyboard.
 */
function PlanCarousel({ children }: { children: React.ReactNode }) {
  const scroller = useRef<HTMLDivElement | null>(null);
  const [atStart, setAtStart] = useState(true);
  const [atEnd, setAtEnd] = useState(true);

  const sync = () => {
    const element = scroller.current;

    if (!element) {
      return;
    }

    setAtStart(element.scrollLeft <= 1);
    setAtEnd(
      element.scrollLeft + element.clientWidth >= element.scrollWidth - 1,
    );
  };

  useEffect(() => {
    const element = scroller.current;

    if (!element) {
      return;
    }

    const observer = new ResizeObserver(() => sync());
    observer.observe(element);

    return () => observer.disconnect();
  }, []);

  const page = (direction: 1 | -1) =>
    scroller.current?.scrollBy({
      left: direction * scroller.current.clientWidth * 0.8,
      behavior: "smooth",
    });

  return (
    <div className="relative w-full min-w-0">
      <div
        ref={scroller}
        onScroll={sync}
        tabIndex={0}
        aria-label="Available plans"
        className="flex w-full min-w-0 snap-x gap-4 overflow-x-auto pb-2 [scrollbar-width:none] focus-visible:outline-none [&::-webkit-scrollbar]:hidden"
      >
        {children}
      </div>

      {!atStart && (
        <Button
          type="button"
          size="icon"
          variant="outline"
          aria-label="Show previous plans"
          onClick={() => page(-1)}
          className="absolute -left-3 top-1/2 h-8 w-8 -translate-y-1/2 rounded-full bg-card shadow-md"
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
      )}

      {!atEnd && (
        <Button
          type="button"
          size="icon"
          variant="outline"
          aria-label="Show more plans"
          onClick={() => page(1)}
          className="absolute -right-3 top-1/2 h-8 w-8 -translate-y-1/2 rounded-full bg-card shadow-md"
        >
          <ChevronRight className="h-4 w-4" />
        </Button>
      )}
    </div>
  );
}

/** Plan picker for one platform: buy when there is no subscription, switch when there is. */
function ChangePlanDialog({
  platform,
  open,
  onOpenChange,
  hasSubscription,
  currentPlan,
  onDone,
}: {
  platform: Platform;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  hasSubscription: boolean;
  currentPlan: string;
  onDone: () => void;
}) {
  const { data: plans } = usePlans();
  const [pending, setPending] = useState<string | null>(null);

  const checkout = useApiMutation<{ checkoutUrl: string }, { planCode: string }>(
    "/api/billing/checkout",
    "post",
    {
      onSuccess: (result) => window.location.assign(result.checkoutUrl),
      onError: (errors) => {
        setPending(null);
        toast.error(errors.join(" "));
      },
    },
  );

  const change = useApiAction<boolean, { planCode: string }>({
    onSuccess: () => {
      setPending(null);
      toast.success("Your plan has been changed.");
      onOpenChange(false);
      onDone();
    },
    onError: (errors) => {
      setPending(null);
      toast.error(errors.join(" "));
    },
  });

  const options = plansForPlatform(plans, platform).filter(
    (plan) => !plan.isFree && plan.code !== currentPlan,
  );

  const select = (plan: SubscriptionPlanRow) => {
    setPending(plan.code);

    if (hasSubscription) {
      change.mutate({
        url: "/api/billing/change-plan",
        body: { planCode: plan.code },
      });
      return;
    }

    checkout.mutate({ planCode: plan.code });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[85vh] sm:max-w-4xl">
        <DialogHeader>
          <DialogTitle>
            {hasSubscription ? "Change plan" : "Choose a plan"}
          </DialogTitle>
          <DialogDescription>
            {hasSubscription
              ? "Switching takes effect immediately; the difference is prorated on your next invoice."
              : "Checkout is handled securely by Stripe. Your Free plan stays available until payment is confirmed."}
          </DialogDescription>
        </DialogHeader>

        <PlanCarousel>
          {options.map((plan) => {
            const presentation = planPresentation[plan.code];
            const price = priceLabel(plan);
            const busy = pending === plan.code;

            return (
              <article
                key={plan.code}
                className="flex w-64 shrink-0 snap-start flex-col rounded-xl border border-border/60 p-4"
              >
                <h3 className="font-display text-base font-semibold">
                  {presentation?.name ?? plan.code}
                </h3>
                <div className="mt-1 flex items-baseline gap-1">
                  <span
                    className={`font-display font-bold ${
                      price.period ? "text-xl" : "text-base"
                    }`}
                  >
                    {price.label}
                  </span>
                  {price.period && (
                    <span className="text-xs text-muted-foreground">
                      {price.period}
                    </span>
                  )}
                </div>
                <p className="mt-2 min-h-8 text-xs text-muted-foreground">
                  {presentation?.tagline}
                </p>
                <ul className="mt-3 space-y-1.5 border-t border-border/60 pt-3">
                  {planFeatures(plan, platform)
                    .slice(0, 4)
                    .map((feature) => (
                      <li
                        key={feature}
                        className="flex items-start gap-2 text-xs text-muted-foreground"
                      >
                        <CheckCircle2 className="mt-0.5 h-3.5 w-3.5 flex-shrink-0 text-success" />
                        {feature}
                      </li>
                    ))}
                </ul>

                <div className="flex-1" />

                {plan.requiresContactSales ? (
                  <Button
                    variant="outline"
                    size="sm"
                    className="mt-4 w-full font-semibold"
                    asChild
                  >
                    <a href="mailto:sales@ledgance.com?subject=Enterprise%20plan">
                      Contact sales
                    </a>
                  </Button>
                ) : plan.purchasable ? (
                  <Button
                    size="sm"
                    className="mt-4 w-full font-semibold"
                    disabled={busy}
                    onClick={() => select(plan)}
                  >
                    {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
                    {hasSubscription ? "Switch to this plan" : "Subscribe"}
                  </Button>
                ) : (
                  <p className="mt-4 text-center text-xs text-muted-foreground">
                    Pricing announced at launch.
                  </p>
                )}
              </article>
            );
          })}
        </PlanCarousel>
      </DialogContent>
    </Dialog>
  );
}

function PlanPanel({ platform }: { platform: Platform }) {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const { data: plans, isLoading } = usePlans();
  const overview = useBillingOverview(!!session && !session.needsOnboarding);
  const queryClient = useQueryClient();
  const [pickerOpen, setPickerOpen] = useState(false);

  const canManage = session?.permissions.includes("organization:billing:manage") ?? false;
  const moduleName = moduleOf(platform);
  const state = billingStateFor(overview.data, platform);
  const planCode =
    state?.plan ??
    session?.plans.find((plan) => plan.module === moduleName)?.plan ??
    "Free";
  const plan = plans?.find((row) => row.code === planCode);
  const presentation = planPresentation[planCode];

  const Icon = platform === "accounting" ? Calculator : ShieldCheck;
  const accent = platform === "accounting" ? "text-emerald-500" : "text-sky-500";

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ["billing-overview"] });
    queryClient.invalidateQueries({ queryKey: ["session"] });
  };

  const portal = useApiMutation<{ portalUrl: string }, { module: string }>(
    "/api/billing/portal",
    "post",
    {
      onSuccess: (result) => window.location.assign(result.portalUrl),
      onError: (errors) => toast.error(errors.join(" ")),
    },
  );

  const cancellation = useApiAction<
    boolean,
    { module: string; cancelAtPeriodEnd: boolean }
  >({
    onSuccess: () => {
      toast.success("Subscription updated.");
      refresh();
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  return (
    <Card className="border-border/60">
      <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
        <CardTitle className="flex items-center gap-2.5 font-display text-base font-semibold">
          <Icon className={`h-5 w-5 ${accent}`} />
          Ledgance {moduleName}
        </CardTitle>
        {state ? (
          <Badge className={`pointer-events-none ${statusTone(state)}`}>
            {presentation?.name ?? planCode} · {statusLabel(state)}
          </Badge>
        ) : (
          <Badge variant={planCode === "Free" ? "secondary" : "default"}>
            {presentation?.name ?? planCode}
          </Badge>
        )}
      </CardHeader>
      <CardContent>
        {state?.status === "PastDue" && (
          <div className="mb-4 flex items-start gap-2.5 rounded-xl border border-destructive/40 bg-destructive/5 p-3 text-sm">
            <TriangleAlert className="mt-0.5 h-4 w-4 flex-shrink-0 text-destructive" />
            <div>
              <p className="font-medium">The last payment failed</p>
              <p className="text-xs text-muted-foreground">
                Update your payment method to keep this plan active.
              </p>
            </div>
          </div>
        )}

        {state?.hasSubscription && state.currentPeriodEnd && (
          <p className="mb-4 flex items-center gap-2 text-xs text-muted-foreground">
            <CalendarClock className="h-3.5 w-3.5" />
            {state.cancelAtPeriodEnd
              ? `Access ends ${fmtRenewal(state.currentPeriodEnd)}`
              : `Renews ${fmtRenewal(state.currentPeriodEnd)}`}
          </p>
        )}

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

        <div className="mt-5 flex flex-wrap items-center gap-2">
          {canManage ? (
            <>
              <Button
                size="sm"
                variant={state?.hasSubscription ? "outline" : "default"}
                className="font-semibold"
                onClick={() => setPickerOpen(true)}
              >
                {state?.hasSubscription ? "Change plan" : "Upgrade"}
                <ArrowUpRight className="ml-2 h-4 w-4" />
              </Button>

              {state?.hasBillingAccount && (
                <Button
                  size="sm"
                  variant="outline"
                  className="font-semibold"
                  disabled={portal.isPending}
                  onClick={() => portal.mutate({ module: moduleName })}
                >
                  {portal.isPending ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <CreditCard className="mr-2 h-4 w-4" />
                  )}
                  Payment methods & invoices
                </Button>
              )}

              {state?.hasSubscription && (
                <Button
                  size="sm"
                  variant="ghost"
                  className="text-muted-foreground"
                  disabled={cancellation.isPending}
                  onClick={() =>
                    cancellation.mutate({
                      url: "/api/billing/cancel",
                      body: {
                        module: moduleName,
                        cancelAtPeriodEnd: !state.cancelAtPeriodEnd,
                      },
                    })
                  }
                >
                  {state.cancelAtPeriodEnd
                    ? "Resume subscription"
                    : "Cancel subscription"}
                </Button>
              )}
            </>
          ) : (
            <Link href={`/pricing?platform=${platform}`}>
              <Button variant="outline" size="sm" className="font-semibold">
                Compare plans
                <ArrowUpRight className="ml-2 h-4 w-4" />
              </Button>
            </Link>
          )}
        </div>

        {!canManage && (
          <p className="mt-3 text-xs text-muted-foreground">
            Only the organization owner can change the subscription.
          </p>
        )}

        <ChangePlanDialog
          platform={platform}
          open={pickerOpen}
          onOpenChange={setPickerOpen}
          hasSubscription={state?.hasSubscription ?? false}
          currentPlan={planCode}
          onDone={refresh}
        />
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
  const moduleName = moduleOf(platform);
  const Icon = platform === "accounting" ? Calculator : ShieldCheck;
  const accent = platform === "accounting" ? "text-emerald-500" : "text-sky-500";

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
        Payments are processed by Stripe. Plan limits shown here are read from
        the server and enforced by the server — a subscription becomes active
        only when Stripe confirms the payment.
      </p>
    </div>
  );
}
