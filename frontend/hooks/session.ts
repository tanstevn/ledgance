import { useApiQuery } from "@/hooks/query";

export interface SessionModulePlan {
  module: "Audit" | "Accounting";
  plan: string;
  requiresContactSales: boolean;
}

export interface Session {
  userId: string;
  email: string;
  organizationId: string;
  role: string;
  permissions: string[];
  plans: SessionModulePlan[];
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
