"use client";

import { useState } from "react";
import { Bot, Loader2, Send, Sparkles } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import { FieldSelect } from "@/components/workspace";
import {
  AgentReportCard,
  CapabilityGrid,
  ProposalCard,
  ToolCard,
  capabilityIncluded,
  useAiCapabilities,
  type AgentReport,
  type AiProposal,
} from "@/components/ai/ai-common";
import { useApiAction, useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import type { EntityRow } from "@/lib/accounting-types";

export default function AccountingAiPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const ready =
    !!session &&
    !session.needsOnboarding &&
    isPlatformEnabled(session, "accounting");

  const capabilities = useAiCapabilities("accounting", ready);
  const entities = useApiQuery<EntityRow[]>("/api/accounting/entities", {
    queryKey: ["accounting-entities"],
    enabled: ready,
  });

  const [question, setQuestion] = useState("");
  const [entityId, setEntityId] = useState("");
  const [goal, setGoal] = useState("");
  const [agentEntityId, setAgentEntityId] = useState("");
  const [proposal, setProposal] = useState<AiProposal | null>(null);
  const [report, setReport] = useState<AgentReport | null>(null);

  const ask = useApiAction<AiProposal>({
    onSuccess: (result) => {
      setProposal(result);
      setReport(null);
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const runAgent = useApiAction<AgentReport>({
    onSuccess: (result) => {
      setReport(result);
      setProposal(null);
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const activeEntities = (entities.data ?? []).filter(
    (entity) => !entity.isArchived,
  );

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="flex items-center gap-2.5 font-display text-2xl font-bold tracking-tight">
          <Sparkles className="h-6 w-6 text-emerald-500" />
          Accounting AI
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Ask questions and investigate your books — proposals only, nothing is
          ever written to the ledger without you. Entity-specific tools live in
          each entity&apos;s AI tab.
        </p>
      </div>

      <Card className="border-border/60">
        <CardHeader className="pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Ask the accounting assistant
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1.5">
            <Label>Question</Label>
            <Textarea
              rows={3}
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="When should I accrue an expense instead of recording it on payment?"
            />
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-64 space-y-1.5">
              <Label>Entity context (optional)</Label>
              <FieldSelect
                value={entityId}
                onChange={(e) => setEntityId(e.target.value)}
              >
                <option value="">General accounting knowledge</option>
                {activeEntities.map((entity) => (
                  <option key={entity.id} value={entity.id}>
                    {entity.name} ({entity.baseCurrency})
                  </option>
                ))}
              </FieldSelect>
            </div>
            <Button
              className="font-semibold"
              disabled={!question.trim() || ask.isPending}
              onClick={() =>
                ask.mutate({
                  url: "/api/accounting/ai/assistant",
                  body: {
                    question: question.trim(),
                    entityId: entityId || null,
                  },
                })
              }
            >
              {ask.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Send className="mr-2 h-4 w-4" />
              )}
              Ask
            </Button>
          </div>
        </CardContent>
      </Card>

      <ToolCard
        title="Agent investigation"
        description="A multi-step AI agent that reads the entity's books through authorized, read-only tools and reports back with its full trail."
        included={capabilityIncluded(capabilities.data, "accounting.agent")}
        tier="agentic"
        platform="accounting"
      >
        <div className="space-y-3">
          <FieldSelect
            value={agentEntityId}
            onChange={(e) => setAgentEntityId(e.target.value)}
          >
            <option value="">Choose an entity…</option>
            {activeEntities.map((entity) => (
              <option key={entity.id} value={entity.id}>
                {entity.name} ({entity.baseCurrency})
              </option>
            ))}
          </FieldSelect>
          <div className="flex gap-2">
            <Textarea
              rows={2}
              value={goal}
              onChange={(e) => setGoal(e.target.value)}
              placeholder="Find out why the March bank reconciliation does not close."
            />
            <Button
              className="self-end font-semibold"
              disabled={!goal.trim() || !agentEntityId || runAgent.isPending}
              onClick={() =>
                runAgent.mutate({
                  url: `/api/accounting/ai/entities/${agentEntityId}/agent`,
                  body: { goal: goal.trim() },
                })
              }
            >
              {runAgent.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Bot className="h-4 w-4" />
              )}
            </Button>
          </div>
        </div>
      </ToolCard>

      {proposal && <ProposalCard proposal={proposal} />}
      {report && <AgentReportCard report={report} />}

      <div>
        <h2 className="font-display text-lg font-semibold">
          What your plan includes
        </h2>
        <p className="mb-4 mt-1 text-sm text-muted-foreground">
          The server enforces these — upgrading unlocks the locked ones
          instantly.
        </p>
        <CapabilityGrid platform="accounting" enabled={ready} />
      </div>
    </div>
  );
}
