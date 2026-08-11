"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import { entitlementRequiredEvent } from "@/util/http";

/**
 * Turns a server-side entitlement refusal (HTTP 402) into one upgrade offer. The server has
 * already declined the operation by this point — this only shows the way forward.
 */
export function UpgradePrompt() {
  const router = useRouter();

  useEffect(() => {
    const handler = (event: Event) => {
      const detail = (event as CustomEvent<{ message: string }>).detail;

      toast.error(detail?.message ?? "Your plan does not include this.", {
        description: "Upgrading raises this limit.",
        action: {
          label: "See plans",
          onClick: () => router.push("/dashboard/billing"),
        },
      });
    };

    window.addEventListener(entitlementRequiredEvent, handler);
    return () => window.removeEventListener(entitlementRequiredEvent, handler);
  }, [router]);

  return null;
}
