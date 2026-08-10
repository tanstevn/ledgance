"use client";

import { useState, useSyncExternalStore } from "react";
import Link from "next/link";
import { Calculator, Link2, ShieldCheck, X } from "lucide-react";
import { Button } from "@/components/ui/button";
import { crossSellTarget, useSession } from "@/hooks/session";
import type { Platform } from "@/lib/plans";

const DISMISS_KEY = "ledgance-cross-sell-dismissed";

const emptySubscribe = () => () => {};

/** Reads the stored dismissal; the server snapshot hides the card until hydration. */
const useStoredDismissal = () =>
  useSyncExternalStore(
    emptySubscribe,
    () => localStorage.getItem(DISMISS_KEY) === "true",
    () => true,
  );

const content: Record<
  Platform,
  {
    icon: typeof Calculator;
    accent: string;
    title: string;
    body: string;
    exploreHref: string;
    plansHref: string;
  }
> = {
  audit: {
    icon: ShieldCheck,
    accent: "text-sky-500",
    title: "Also being audited — or running audits?",
    body:
      "Ledgance Audit manages the complete audit lifecycle. And because your books already live in Ledgance Accounting, your organization can optionally let audit engagements read them directly — trial balances with full provenance, no exports.",
    exploreHref: "/audit",
    plansHref: "/pricing?platform=audit",
  },
  accounting: {
    icon: Calculator,
    accent: "text-emerald-500",
    title: "Want internal accounting context for your audits?",
    body:
      "Ledgance Accounting keeps real double-entry books. When your organization runs both platforms, engagements can read the organization's own books directly — an optional complement, never a requirement. External trial balance imports keep working either way.",
    exploreHref: "/accounting",
    plansHref: "/pricing?platform=accounting",
  },
};

/**
 * Shown only after the backend confirms a qualifying paid subscription on the other
 * platform. Always optional: explore, subscribe, maybe later, or dismiss for good.
 */
export function CrossSellCard({ onSkip }: { onSkip?: () => void }) {
  const { data: session } = useSession();
  const [hidden, setHidden] = useState(false);
  const storedDismissal = useStoredDismissal();

  const target = crossSellTarget(session);
  if (!target || storedDismissal || hidden) return null;

  const item = content[target];
  const Icon = item.icon;

  const dismissForGood = () => {
    localStorage.setItem(DISMISS_KEY, "true");
    setHidden(true);
    onSkip?.();
  };

  const maybeLater = () => {
    setHidden(true);
    onSkip?.();
  };

  return (
    <div className="relative overflow-hidden rounded-2xl border border-border/60 bg-card p-6">
      <div className="pointer-events-none absolute right-0 top-0 h-32 w-32 rounded-full bg-primary/5 blur-2xl" />
      <button
        onClick={dismissForGood}
        aria-label="Don't show this again"
        className="absolute right-4 top-4 rounded-md p-1 text-muted-foreground transition-colors hover:bg-muted hover:text-foreground"
      >
        <X className="h-4 w-4" />
      </button>
      <div className="flex items-start gap-4">
        <div className="flex h-11 w-11 flex-shrink-0 items-center justify-center rounded-xl bg-muted">
          <Icon className={`h-5.5 w-5.5 ${item.accent}`} />
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <Link2 className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
              Optional — works great without it
            </span>
          </div>
          <h3 className="mt-1.5 font-display text-lg font-semibold">
            {item.title}
          </h3>
          <p className="mt-2 text-sm leading-relaxed text-muted-foreground">
            {item.body}
          </p>
          <div className="mt-4 flex flex-wrap items-center gap-3">
            <Link href={item.plansHref}>
              <Button size="sm" className="font-semibold">
                See {target === "audit" ? "Audit" : "Accounting"} plans
              </Button>
            </Link>
            <Link href={item.exploreHref}>
              <Button size="sm" variant="outline" className="font-semibold">
                Explore first
              </Button>
            </Link>
            <Button
              size="sm"
              variant="ghost"
              className="text-muted-foreground"
              onClick={maybeLater}
            >
              Maybe later
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
