"use client";

import { useState } from "react";
import Link from "next/link";
import {
  Bot,
  ChevronDown,
  ChevronRight,
  Loader2,
  Lock,
  Play,
  Sparkles,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { useApiQuery } from "@/hooks/query";
import type { Platform } from "@/lib/plans";
import { cn } from "@/lib/utils";

export interface AiProposal {
  capability: string;
  content: string;
  provider: string;
  model: string;
  tier: string;
  disclaimer: string;
}

export interface AgentStepView {
  tool: string;
  arguments: string;
  result: string;
}

export interface AgentReport {
  capability: string;
  answer: string;
  steps: AgentStepView[];
  provider: string;
  model: string;
  turnsUsed: number;
  disclaimer: string;
}

export interface AiCapabilityRow {
  key: string;
  description: string;
  requiredTier: string;
  included: boolean;
}

export const useAiCapabilities = (platform: Platform, enabled: boolean) =>
  useApiQuery<AiCapabilityRow[]>(`/api/${platform}/ai/capabilities`, {
    queryKey: [`${platform}-ai-capabilities`],
    enabled,
  });

export const tierLabels: Record<string, string> = {
  basic: "Free",
  advanced: "Paid plans",
  reasoning: "Higher plans",
  agentic: "Top plans",
};

export function TierBadge({ tier }: { tier: string }) {
  return (
    <Badge variant="outline" className="pointer-events-none text-[10px] capitalize">
      {tier}
    </Badge>
  );
}

/** AI output is a proposal: content plus provenance and a permanent disclaimer. */
export function ProposalCard({ proposal }: { proposal: AiProposal }) {
  return (
    <div className="rounded-2xl border border-border/60 bg-card">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 px-4 py-2.5">
        <div className="flex items-center gap-2 text-sm font-semibold">
          <Sparkles className="h-4 w-4 text-primary" />
          AI proposal
        </div>
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <TierBadge tier={proposal.tier} />
          {proposal.provider} · {proposal.model}
        </div>
      </div>
      <div className="whitespace-pre-wrap px-4 py-4 text-sm leading-relaxed">
        {proposal.content}
      </div>
      <p className="border-t border-border/60 px-4 py-2.5 text-xs text-muted-foreground">
        {proposal.disclaimer}
      </p>
    </div>
  );
}

export function AgentReportCard({ report }: { report: AgentReport }) {
  const [showSteps, setShowSteps] = useState(false);

  return (
    <div className="rounded-2xl border border-border/60 bg-card">
      <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 px-4 py-2.5">
        <div className="flex items-center gap-2 text-sm font-semibold">
          <Bot className="h-4 w-4 text-primary" />
          Agent investigation
        </div>
        <div className="text-xs text-muted-foreground">
          {report.provider} · {report.model} · {report.turnsUsed} turns
        </div>
      </div>
      <div className="whitespace-pre-wrap px-4 py-4 text-sm leading-relaxed">
        {report.answer}
      </div>
      {report.steps.length > 0 && (
        <div className="border-t border-border/60 px-4 py-2.5">
          <button
            onClick={() => setShowSteps((v) => !v)}
            className="flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground"
          >
            {showSteps ? (
              <ChevronDown className="h-3.5 w-3.5" />
            ) : (
              <ChevronRight className="h-3.5 w-3.5" />
            )}
            {report.steps.length} tool step{report.steps.length === 1 ? "" : "s"}{" "}
            — what the agent read
          </button>
          {showSteps && (
            <div className="mt-2 space-y-2">
              {report.steps.map((step, index) => (
                <div
                  key={index}
                  className="rounded-lg bg-muted/40 px-3 py-2 text-xs"
                >
                  <div className="font-mono font-semibold">{step.tool}</div>
                  <div className="mt-1 line-clamp-3 whitespace-pre-wrap text-muted-foreground">
                    {step.result}
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
      <p className="border-t border-border/60 px-4 py-2.5 text-xs text-muted-foreground">
        {report.disclaimer}
      </p>
    </div>
  );
}

/**
 * The capability catalog: what this plan includes and what upgrades unlock. The backend's
 * `included` flag is the source of truth — the UI only renders it.
 */
export function CapabilityGrid({
  platform,
  enabled,
}: {
  platform: Platform;
  enabled: boolean;
}) {
  const capabilities = useAiCapabilities(platform, enabled);

  if (capabilities.isLoading || !enabled) {
    return (
      <div className="grid gap-3 sm:grid-cols-2">
        {[1, 2, 3, 4].map((i) => (
          <Skeleton key={i} className="h-16 rounded-xl" />
        ))}
      </div>
    );
  }

  if (capabilities.isError || !capabilities.data) return null;

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {capabilities.data.map((capability) => (
        <div
          key={capability.key}
          className={cn(
            "flex items-start gap-3 rounded-xl border px-4 py-3",
            capability.included
              ? "border-border/60 bg-card"
              : "border-dashed border-border bg-muted/20",
          )}
        >
          {capability.included ? (
            <Sparkles className="mt-0.5 h-4 w-4 flex-shrink-0 text-primary" />
          ) : (
            <Lock className="mt-0.5 h-4 w-4 flex-shrink-0 text-muted-foreground" />
          )}
          <div className="min-w-0 flex-1">
            <div
              className={cn(
                "text-sm font-medium",
                !capability.included && "text-muted-foreground",
              )}
            >
              {capability.description}
            </div>
            {!capability.included && (
              <Link
                href={`/pricing?platform=${platform}`}
                className="mt-0.5 inline-block text-xs font-medium text-primary hover:underline"
              >
                Unlocks with the {tierLabels[capability.requiredTier] ?? "paid"}{" "}
                tier →
              </Link>
            )}
          </div>
          <TierBadge tier={capability.requiredTier} />
        </div>
      ))}
    </div>
  );
}

export const capabilityIncluded = (
  capabilities: AiCapabilityRow[] | undefined,
  key: string,
) => capabilities?.find((capability) => capability.key === key)?.included ?? false;

/** The trigger for one AI tool: spins while its own run is in flight. */
export function AiRunButton({
  label,
  onClick,
  busy,
  spinning,
  disabled = false,
}: {
  label: string;
  onClick: () => void;
  busy: boolean;
  spinning: boolean;
  disabled?: boolean;
}) {
  return (
    <Button
      size="sm"
      className="font-semibold"
      disabled={busy || disabled}
      onClick={onClick}
    >
      {spinning ? (
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
      ) : (
        <Play className="mr-2 h-3.5 w-3.5" />
      )}
      {label}
    </Button>
  );
}

/** One AI action: runnable when the plan includes it, locked with an upgrade path when not. */
export function ToolCard({
  title,
  description,
  included,
  tier,
  platform,
  children,
}: {
  title: string;
  description: string;
  included: boolean;
  tier: string;
  platform: Platform;
  children: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        "rounded-xl border p-4",
        included
          ? "border-border/60 bg-card"
          : "border-dashed border-border bg-muted/20",
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <div>
          <div
            className={cn(
              "text-sm font-semibold",
              !included && "text-muted-foreground",
            )}
          >
            {title}
          </div>
          <p className="mt-0.5 text-xs text-muted-foreground">{description}</p>
        </div>
        <TierBadge tier={tier} />
      </div>
      {included ? (
        <div className="mt-3">{children}</div>
      ) : (
        <Link
          href={`/pricing?platform=${platform}`}
          className="mt-3 inline-flex items-center gap-1.5 text-xs font-medium text-primary hover:underline"
        >
          <Lock className="h-3 w-3" />
          Unlocks with the {tierLabels[tier] ?? "paid"} tier →
        </Link>
      )}
    </div>
  );
}
