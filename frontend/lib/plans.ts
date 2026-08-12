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
  AuditMicro: {
    name: "Micro",
    tagline: "A working practice: real capacity and AI that drafts alongside you.",
    price: { label: "Pricing at launch" },
    highlighted: true,
  },
  AuditMicroGrowth: {
    name: "Micro-Growth",
    tagline: "AI that reads the whole engagement and writes the whole draft report.",
    price: { label: "Pricing at launch" },
  },
  AuditSmall: {
    name: "Small",
    tagline: "Engagement-wide reporting, anomaly detection and review assistance.",
    price: { label: "Pricing at launch" },
  },
  AuditMedium: {
    name: "Medium",
    tagline: "Firm-level intelligence across clients, periods and engagements.",
    price: { label: "Pricing at launch" },
  },
  AuditMediumGrowth: {
    name: "Medium-Growth",
    tagline: "Agentic audit AI: it gathers, drafts, then checks its own work.",
    price: { label: "Pricing at launch" },
  },
  AuditEnterprise: {
    name: "Enterprise",
    tagline: "Your methodology, your templates, your governance.",
    price: { label: "Contact sales" },
  },
};

const formatCount = (value: string | undefined) => {
  if (value === "-1") return "Unlimited";
  const numeric = Number(value ?? 0);
  return Number.isFinite(numeric) ? numeric.toLocaleString() : "0";
};

export const isUnlimited = (value: string | undefined) => value === "-1";

/**
 * What a plan's AI capacity is for, in one line. AI credits are a product measure — an
 * operation costs what it is worth, so a whole report draws far more than a question — and a
 * customer should understand the value without doing the arithmetic.
 */
const aiCapacitySummary: Record<string, string> = {
  Free: "Explore Audit AI with limited monthly usage.",
  AuditMicro: "AI assistance for everyday audit work.",
  AuditMicroGrowth: "Advanced AI analysis and complete draft report generation.",
  AuditSmall: "Expanded AI workflows for growing audit teams.",
  AuditMedium: "Advanced firm-level AI intelligence.",
  AuditMediumGrowth: "High-capacity agentic Audit AI.",
  AuditEnterprise: "Custom AI capacity and governance.",
};

export const planAiCapacity = (plan: SubscriptionPlanRow): string | null =>
  aiCapacitySummary[plan.code] ?? null;

/** AI credits read as a plain count; unlimited says so. */
export const formatCredits = (value: string | number | undefined) =>
  value === "-1" || value === -1 ? "Unlimited" : Number(value ?? 0).toLocaleString();

/** Bytes to the unit a person would say out loud. */
export function formatBytes(value: string | number | undefined): string {
  if (value === "-1" || value === -1) return "Unlimited";

  const bytes = Number(value ?? 0);
  if (!Number.isFinite(bytes)) return "0 GB";

  const gb = bytes / (1024 * 1024 * 1024);
  if (gb >= 1024) {
    const tb = gb / 1024;
    return `${Number.isInteger(tb) ? tb : tb.toFixed(1)} TB`;
  }

  return `${Number.isInteger(gb) ? gb : gb.toFixed(1)} GB`;
}

const plural = (value: string, singular: string, pluralWord?: string) =>
  value === "1" ? singular : (pluralWord ?? `${singular}s`);

export interface PlanCapacityRow {
  label: string;
  value: string;
  /** The entitlement key, so a usage reading can be matched to the right row. */
  key: string;
}

/**
 * The capacity a plan buys, in the order an audit team compares it. Read straight from the
 * server's entitlement values so the ceiling shown is the ceiling enforced.
 */
export function planCapacity(
  plan: SubscriptionPlanRow,
  platform: Platform,
): PlanCapacityRow[] {
  const e = plan.entitlements;

  const rows: PlanCapacityRow[] = [
    { key: "max_users", label: "Users", value: formatCount(e["max_users"]) },
  ];

  if (platform === "accounting") {
    rows.push(
      {
        key: "max_entities",
        label: "Entities",
        value: formatCount(e["max_entities"]),
      },
      {
        key: "max_transactions_per_period",
        label: "Transactions / period",
        value: formatCount(e["max_transactions_per_period"]),
      },
    );
  } else {
    rows.push(
      { key: "max_clients", label: "Clients", value: formatCount(e["max_clients"]) },
      {
        key: "max_engagements",
        label: "Engagements",
        value: formatCount(e["max_engagements"]),
      },
    );
  }

  rows.push({
    key: "storage_bytes",
    label: "Storage",
    value: formatBytes(e["storage_bytes"]),
  });

  return rows;
}

/**
 * What AI can actually do on a plan, derived from the same entitlement values the backend
 * gates on: the reasoning tier, how complete a report it may generate, and how far across the
 * record set it may reason. Nothing here is advertised that the server would refuse.
 */
export function planAiCapabilities(
  plan: SubscriptionPlanRow,
  plans?: SubscriptionPlanRow[],
): string[] {
  const e = plan.entitlements;
  const tier = e["ai_max_tier"] ?? "basic";
  const reports = e["ai_report_scope"] ?? "none";
  const analysis = e["ai_analysis_scope"] ?? "document";
  const capabilities: string[] = [];

  const below = previousPlan(plan, plans);
  const belowName = previousPlanName(plan);

  // "Everything in <plan below>" already covers anything the step down also had, so each
  // bullet is emitted only where the entitlement driving it actually moved.
  const gained = (key: string) =>
    !below || below.entitlements[key] !== e[key];

  if (belowName) capabilities.push(`Everything in ${belowName}`);

  if (!belowName) {
    capabilities.push(
      "AI audit assistant",
      "Finding and engagement summaries",
      "AI-generated notes and wording help",
      "Basic document analysis",
    );
  }

  if (tier === "advanced" && reports === "sections" && gained("ai_max_tier")) {
    capabilities.push(
      "Audit planning and materiality assistance",
      "Risk and procedure suggestions",
      "Working-paper and finding drafting",
    );
  }

  if (gained("ai_analysis_scope")) {
    if (analysis === "engagement") capabilities.push("Engagement-wide intelligence");
    if (analysis === "workflow") capabilities.push("Multi-step AI audit workflows");
    if (analysis === "portfolio") {
      capabilities.push("Multi-engagement, client and firm intelligence");
    }
  }

  if (gained("ai_report_scope")) {
    if (reports === "sections") capabilities.push("AI-generated report sections");
    if (reports === "full_draft") {
      capabilities.push("Complete draft audit reports", "Evidence and gap analysis");
    }
    if (reports === "engagement") {
      capabilities.push(
        "Full engagement report generation",
        "Management and reviewer drafts",
      );
    }
    if (reports === "portfolio") capabilities.push("Client and firm-level reporting");
    if (reports === "agentic") {
      capabilities.push("Agentic audit workflows", "Agentic report generation");
    }
    if (reports === "custom") {
      capabilities.push(
        "Custom AI agents and report templates",
        "Your audit methodology and terminology",
        "Enterprise AI governance and controls",
      );
    }
  }

  if (
    tier === "reasoning" &&
    e["advanced_review"] === "true" &&
    gained("advanced_review")
  ) {
    capabilities.push("Anomaly detection and review assistance");
  }

  const units = formatCount(e["ai_monthly_units"]);
  capabilities.push(
    units === "Unlimited"
      ? "Unlimited AI credits per billing period"
      : `${units} AI credits per billing period`,
  );

  return capabilities;
}

/**
 * Turns a plan's entitlement map into the feature bullets shown on pricing surfaces, so
 * marketing never drifts from what the backend actually authorizes.
 */
export function planFeatures(
  plan: SubscriptionPlanRow,
  platform: Platform,
  plans?: SubscriptionPlanRow[],
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

  features.push(`${formatBytes(e["storage_bytes"])} storage`);

  if (platform === "audit") {
    features.push(...planAiCapabilities(plan, plans));
  } else {
    const aiUnits = formatCount(e["ai_monthly_units"]);
    features.push(
      aiUnits === "Unlimited"
        ? "Unlimited AI actions per month"
        : `${aiUnits} AI actions per month`,
      accountingAiTierLabels[e["ai_max_tier"] ?? "basic"] ?? "AI assistance",
    );

    if (e["advanced_analysis"] === "true") features.push("Advanced analysis");
    if (e["advanced_review"] === "true") features.push("Advanced review workflows");
    if (e["automation"] === "true") features.push("Workflow automation");
    if (e["integrations"] === "true") features.push("Integrations");
    if (e["api_access"] === "true") features.push("API access");
  }

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

const accountingAiTierLabels: Record<string, string> = {
  basic: "Essential AI assistance",
  advanced: "Advanced AI assistance",
  reasoning: "Advanced reasoning AI",
  agentic: "Agentic AI investigation",
};

/**
 * Cheapest first within each product, with the shared Free plan leading. This mirrors the
 * server's own ordering, which decides what "the next plan up" means.
 */
const planOrder = [
  "Free",
  "AuditMicro",
  "AuditMicroGrowth",
  "AuditSmall",
  "AuditMedium",
  "AuditMediumGrowth",
  "AuditEnterprise",
  "AccountingSolo",
  "AccountingTeam",
  "AccountingProfessional",
  "AccountingEnterprise",
];

export function plansForPlatform(
  plans: SubscriptionPlanRow[] | undefined,
  platform: Platform,
): SubscriptionPlanRow[] {
  if (!plans) return [];

  const moduleName = platform === "accounting" ? "Accounting" : "Audit";

  return plans
    .filter((plan) => plan.isFree || plan.module === moduleName)
    .sort((a, b) => planOrder.indexOf(a.code) - planOrder.indexOf(b.code));
}

const orderedFor = (moduleName: "Audit" | "Accounting") =>
  planOrder.filter(
    (code) =>
      code === "Free" ||
      (moduleName === "Accounting"
        ? code.startsWith("Accounting")
        : code.startsWith("Audit")),
  );

const previousCode = (plan: SubscriptionPlanRow): string | null => {
  const ordered = orderedFor(plan.module);
  const index = ordered.indexOf(plan.code);

  return index > 0 ? ordered[index - 1] : null;
};

/** The display name of the plan one step below this one, for "Everything in …". */
function previousPlanName(plan: SubscriptionPlanRow): string | null {
  const code = previousCode(plan);
  return code ? (planPresentation[code]?.name ?? null) : null;
}

/** The plan one step below, when the caller has the catalogue to look it up in. */
function previousPlan(
  plan: SubscriptionPlanRow,
  plans: SubscriptionPlanRow[] | undefined,
): SubscriptionPlanRow | undefined {
  const code = previousCode(plan);
  return code ? plans?.find((row) => row.code === code) : undefined;
}

/** Where a plan sits in its product's ladder; -1 when the code is unknown. */
export const planRank = (planCode: string, moduleName: "Audit" | "Accounting") =>
  orderedFor(moduleName).indexOf(planCode);

/** The plan one step up from the current one, or null at the top of the ladder. */
export function nextPlanUp(
  plans: SubscriptionPlanRow[] | undefined,
  platform: Platform,
  currentCode: string,
): SubscriptionPlanRow | null {
  const moduleName = platform === "accounting" ? "Accounting" : "Audit";
  const ordered = orderedFor(moduleName);
  const index = ordered.indexOf(currentCode);

  if (index < 0 || index + 1 >= ordered.length) return null;

  return plans?.find((plan) => plan.code === ordered[index + 1]) ?? null;
}

/**
 * Cross-platform offers apply only to qualifying paid plans: never Free and never Enterprise.
 * Mirrors the product rules; the backend remains the enforcement layer.
 */
export const crossSellQualifyingPlans: Record<Platform, string[]> = {
  accounting: ["AccountingSolo", "AccountingTeam", "AccountingProfessional"],
  audit: [
    "AuditMicro",
    "AuditMicroGrowth",
    "AuditSmall",
    "AuditMedium",
    "AuditMediumGrowth",
  ],
};

export const platformOf = (planCode: string): Platform =>
  planCode.startsWith("Accounting") ? "accounting" : "audit";

export const isPaidPlan = (planCode: string) => planCode !== "Free";
