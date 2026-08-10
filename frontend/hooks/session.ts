import { useApiQuery } from "@/hooks/query";
import {
  crossSellQualifyingPlans,
  isPaidPlan,
  type Platform,
} from "@/lib/plans";

export interface SessionModulePlan {
  module: "Audit" | "Accounting";
  plan: string;
  requiresContactSales: boolean;
}

export interface Session {
  userId: string;
  email: string;
  organizationId: string | null;
  organizationName: string | null;
  role: string;
  permissions: string[];
  plans: SessionModulePlan[];
  products: string[];
  needsOnboarding: boolean;
}

/**
 * Server-resolved identity, organization and plan context. Use it to render, never to
 * authorize — the API re-checks every gated operation.
 */
export const useSession = (enabled = true) =>
  useApiQuery<Session>("/api/session", {
    queryKey: ["session"],
    enabled,
  });

/**
 * The platforms this organization has activated — server-resolved from the signup choice
 * plus any paid subscriptions. The dashboard renders only these.
 */
export const enabledPlatforms = (session: Session | undefined): Platform[] => {
  if (!session) return [];

  const platforms: Platform[] = [];
  if (session.products.includes("Accounting")) platforms.push("accounting");
  if (session.products.includes("Audit")) platforms.push("audit");
  return platforms;
};

export const isPlatformEnabled = (
  session: Session | undefined,
  platform: Platform,
): boolean => enabledPlatforms(session).includes(platform);

export const planForPlatform = (
  session: Session | undefined,
  platform: Platform,
): SessionModulePlan | undefined =>
  session?.plans.find(
    (plan) => plan.module.toLowerCase() === platform,
  );

export const hasPaidPlan = (
  session: Session | undefined,
  platform: Platform,
): boolean => {
  const plan = planForPlatform(session, platform);
  return !!plan && isPaidPlan(plan.plan);
};

/**
 * A cross-platform offer is shown only when the backend-confirmed subscription on one
 * platform is a qualifying paid plan AND the other platform is not subscribed yet.
 * Clicking an offer never grants anything — subscribing runs the full flow.
 */
export const crossSellTarget = (
  session: Session | undefined,
): Platform | null => {
  if (!session || session.needsOnboarding) return null;

  const accounting = planForPlatform(session, "accounting");
  const audit = planForPlatform(session, "audit");

  if (
    accounting &&
    crossSellQualifyingPlans.accounting.includes(accounting.plan) &&
    audit &&
    !isPaidPlan(audit.plan)
  ) {
    return "audit";
  }

  if (
    audit &&
    crossSellQualifyingPlans.audit.includes(audit.plan) &&
    accounting &&
    !isPaidPlan(accounting.plan)
  ) {
    return "accounting";
  }

  return null;
};
