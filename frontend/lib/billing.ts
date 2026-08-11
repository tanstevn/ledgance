import { useApiQuery } from "@/hooks/query";
import type { Platform } from "@/lib/plans";

export interface BillingProductState {
  module: "Audit" | "Accounting";
  plan: string;
  status: "Active" | "Trialing" | "PastDue" | "Canceled";
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  hasBillingAccount: boolean;
  hasSubscription: boolean;
  requiresContactSales: boolean;
}

export interface BillingOverview {
  products: BillingProductState[];
}

/**
 * Server-resolved billing state. The plan reported here is the one the entitlement service
 * enforces, so what billing shows and what the product allows cannot drift apart.
 */
export const useBillingOverview = (enabled = true) =>
  useApiQuery<BillingOverview>("/api/billing/overview", {
    queryKey: ["billing-overview"],
    enabled,
    retry: false,
  });

export const moduleOf = (platform: Platform): "Audit" | "Accounting" =>
  platform === "accounting" ? "Accounting" : "Audit";

export const billingStateFor = (
  overview: BillingOverview | undefined,
  platform: Platform,
): BillingProductState | undefined =>
  overview?.products.find((product) => product.module === moduleOf(platform));

export const statusLabel = (state: BillingProductState): string => {
  if (state.status === "PastDue") return "Payment failed";
  if (state.status === "Trialing") return "Trial";
  if (state.cancelAtPeriodEnd) return "Ends at period end";
  if (state.status === "Canceled") return "No subscription";
  return "Active";
};

export const fmtRenewal = (value: string | null): string =>
  value
    ? new Date(value).toLocaleDateString(undefined, {
        year: "numeric",
        month: "short",
        day: "numeric",
      })
    : "—";
