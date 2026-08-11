import { useApiQuery } from "@/hooks/query";

export type Platform = "accounting" | "audit";

export interface SubscriptionPlanRow {
  code: string;
  module: "Audit" | "Accounting";
  isFree: boolean;
  requiresContactSales: boolean;
  /** Server-declared: a paid plan with a price configured on the payment provider. */
  purchasable: boolean;
  /** The live price Stripe will charge, in the currency's smallest unit. */
  amountMinorUnits: number | null;
  currency: string | null;
  interval: string | null;
  intervalCount: number | null;
  entitlements: Record<string, string>;
}

/** The server-declared plan catalog — the same source the backend enforces. */
export const usePlans = () =>
  useApiQuery<SubscriptionPlanRow[]>("/api/subscriptions/plans", {
    queryKey: ["subscription-plans"],
  });

/**
 * Presentation metadata keyed by backend plan code — name and positioning only. The price a
 * customer sees comes from `priceLabel`, which reads the live provider price; the `price`
 * field here is the fallback used only when the provider has no price for the plan yet.
 * Never invent prices here.
 */
export interface PlanPresentation {
  name: string;
  tagline: string;
  price: { label: string; period?: string };
  highlighted?: boolean;
}

export interface PriceLabel {
  label: string;
  period?: string;
}

/**
 * What a plan costs, preferring what the payment provider will actually charge over any
 * value written into this file. A plan the provider has no price for reads as unannounced
 * rather than as a guess.
 */
export function priceLabel(plan: SubscriptionPlanRow): PriceLabel {
  const fallback = planPresentation[plan.code]?.price ?? {
    label: "Pricing at launch",
  };

  if (plan.isFree || plan.requiresContactSales) {
    return fallback;
  }

  if (plan.amountMinorUnits === null || !plan.currency) {
    return fallback.period ? fallback : { label: "Pricing at launch" };
  }

  const amount = plan.amountMinorUnits / 100;
  const label = new Intl.NumberFormat(undefined, {
    style: "currency",
    currency: plan.currency,
    minimumFractionDigits: Number.isInteger(amount) ? 0 : 2,
  }).format(amount);

  const count = plan.intervalCount ?? 1;
  const unit = plan.interval ?? "month";
  const period = count > 1 ? `/${count} ${unit}s` : `/${unit}`;

  return { label, period };
}

export const planPresentation: Record<string, PlanPresentation> = {
  Free: {
    name: "Free",
    tagline: "Genuinely useful, free forever. The real product, sized for getting started.",
    price: { label: "$0", period: "forever" },
  },
  AccountingSolo: {
    name: "Solo",
    tagline: "For the individual accountant running real books.",
    price: { label: "$14.99", period: "/month" },
    highlighted: true,
  },
  AccountingTeam: {
    name: "Team",
    tagline: "Collaborative accounting for small teams.",
    price: { label: "Pricing at launch" },
  },
  AccountingProfessional: {
    name: "Professional",
    tagline: "Advanced accounting for professional practices.",
    price: { label: "Pricing at launch" },
  },
  AccountingEnterprise: {
    name: "Enterprise",
    tagline: "Custom limits and terms for enterprise finance.",
    price: { label: "Contact sales" },
  },
  AuditProfessional: {
    name: "Professional",
    tagline: "Expanded capacity and advanced AI for growing practices.",
    price: { label: "Pricing at launch" },
    highlighted: true,
  },
  AuditOrganization: {
    name: "Organization",
    tagline: "Higher capacity, advanced review workflows and automation.",
    price: { label: "Pricing at launch" },
  },
  AuditFirm: {
    name: "Firm",
    tagline: "Large-team capability with agentic AI investigation.",
    price: { label: "Pricing at launch" },
  },
  AuditEnterprise: {
    name: "Enterprise",
    tagline: "Enterprise scale, custom commercial arrangements.",
    price: { label: "Contact sales" },
  },
};

const aiTierLabels: Record<string, string> = {
  basic: "Essential AI assistance",
  advanced: "Advanced AI assistance",
  reasoning: "Advanced reasoning AI",
  agentic: "Agentic AI investigation",
};

const formatCount = (value: string | undefined) => {
  if (value === "-1") return "Unlimited";
  const numeric = Number(value ?? 0);
  return Number.isFinite(numeric) ? numeric.toLocaleString() : "0";
};

const formatStorage = (bytes: string | undefined) => {
  if (bytes === "-1") return "Unlimited storage";
  const gb = Number(bytes ?? 0) / (1024 * 1024 * 1024);
  return gb >= 1024 ? `${gb / 1024} TB storage` : `${gb} GB storage`;
};

const plural = (value: string, singular: string, pluralWord?: string) =>
  value === "1" ? singular : (pluralWord ?? `${singular}s`);

/**
 * Turns a plan's entitlement map into the feature bullets shown on pricing surfaces, so
 * marketing never drifts from what the backend actually authorizes.
 */
export function planFeatures(
  plan: SubscriptionPlanRow,
  platform: Platform,
): string[] {
  const e = plan.entitlements;
  const features: string[] = [];

  const users = formatCount(e["max_users"]);
  features.push(`${users} ${plural(users, "user")}`);

  if (platform === "accounting") {
    const entities = formatCount(e["max_entities"]);
    const transactions = formatCount(e["max_transactions_per_period"]);
    features.push(
      `${entities} accounting ${plural(entities, "entity", "entities")} (sets of books)`,
      `${transactions} transactions per fiscal period`,
    );
  } else {
    const clients = formatCount(e["max_clients"]);
    const engagements = formatCount(e["max_engagements"]);
    features.push(
      `${clients} ${plural(clients, "client")}`,
      `${engagements} ${plural(engagements, "engagement")}`,
    );
  }

  features.push(formatStorage(e["storage_bytes"]));

  const aiUnits = formatCount(e["ai_monthly_units"]);
  features.push(
    aiUnits === "Unlimited"
      ? "Unlimited AI actions per month"
      : `${aiUnits} AI actions per month`,
    aiTierLabels[e["ai_max_tier"] ?? "basic"] ?? "AI assistance",
  );

  if (e["advanced_analysis"] === "true") features.push("Advanced analysis");
  if (e["advanced_review"] === "true") features.push("Advanced review workflows");
  if (e["automation"] === "true") features.push("Workflow automation");
  if (e["integrations"] === "true") features.push("Integrations");
  if (e["api_access"] === "true") features.push("API access");

  if (e["accounting_context_sharing"] === "true") {
    features.push(
      platform === "accounting"
        ? "Share books with Ledgance Audit (optional)"
        : "Connect Ledgance Accounting books (optional)",
    );
  }

  if (e["enterprise_support"] === "true") features.push("Enterprise support");

  return features;
}

export function plansForPlatform(
  plans: SubscriptionPlanRow[] | undefined,
  platform: Platform,
): SubscriptionPlanRow[] {
  if (!plans) return [];

  const moduleName = platform === "accounting" ? "Accounting" : "Audit";
  const order = [
    "Free",
    "AccountingSolo",
    "AccountingTeam",
    "AccountingProfessional",
    "AccountingEnterprise",
    "AuditProfessional",
    "AuditOrganization",
    "AuditFirm",
    "AuditEnterprise",
  ];

  return plans
    .filter((plan) => plan.isFree || plan.module === moduleName)
    .sort((a, b) => order.indexOf(a.code) - order.indexOf(b.code));
}

/**
 * Cross-platform offers apply only to qualifying paid plans: the solo-to-professional
 * range, never Free and never Enterprise. Mirrors the product rules; the backend remains
 * the enforcement layer.
 */
export const crossSellQualifyingPlans: Record<Platform, string[]> = {
  accounting: ["AccountingSolo", "AccountingTeam", "AccountingProfessional"],
  audit: ["AuditProfessional", "AuditOrganization", "AuditFirm"],
};

export const platformOf = (planCode: string): Platform =>
  planCode.startsWith("Accounting") ? "accounting" : "audit";

export const isPaidPlan = (planCode: string) => planCode !== "Free";
