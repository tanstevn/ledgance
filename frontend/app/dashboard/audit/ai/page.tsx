"use client";

import { useState } from "react";
import { Loader2, Send, ShieldCheck, Sparkles } from "lucide-react";
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
import type { EngagementListRow } from "@/lib/audit-types";

export default function AuditAiPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const ready =
    !!session && !session.needsOnboarding && isPlatformEnabled(session, "audit");

  const capabilities = useAiCapabilities("audit", ready);
  const engagements = useApiQuery<EngagementListRow[]>(
    "/api/audit/engagements",
    { queryKey: ["audit-engagements"], enabled: ready },
  );

  const [question, setQuestion] = useState("");
  const [engagementId, setEngagementId] = useState("");
  const [goal, setGoal] = useState("");
  const [agentEngagementId, setAgentEngagementId] = useState("");
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

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="flex items-center gap-2.5 font-display text-2xl font-bold tracking-tight">
          <Sparkles className="h-6 w-6 text-sky-500" />
          Audit AI
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Ask questions, generate proposals and investigate — always grounded in
          your engagements, always yours to review. Engagement-specific tools
          live in each engagement&apos;s AI tab.
        </p>
      </div>

      <Card className="border-border/60">
        <CardHeader className="pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Ask the audit assistant
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="space-y-1.5">
            <Label>Question</Label>
            <Textarea
              rows={3}
              value={question}
              onChange={(e) => setQuestion(e.target.value)}
              placeholder="How should I set performance materiality for a first-year audit?"
            />
          </div>
          <div className="flex flex-wrap items-end gap-3">
            <div className="min-w-64 space-y-1.5">
              <Label>Engagement context (optional)</Label>
              <FieldSelect
                value={engagementId}
                onChange={(e) => setEngagementId(e.target.value)}
              >
                <option value="">General audit methodology</option>
                {(engagements.data ?? []).map((engagement) => (
                  <option key={engagement.id} value={engagement.id}>
                    {engagement.name} — {engagement.clientName}
                  </option>
                ))}
              </FieldSelect>
            </div>
            <Button
              className="font-semibold"
              disabled={!question.trim() || ask.isPending}
              onClick={() =>
                ask.mutate({
                  url: "/api/audit/ai/assistant",
                  body: {
                    question: question.trim(),
                    engagementId: engagementId || null,
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
        description="A multi-step AI agent that reads the engagement through authorized, read-only tools and reports back with its full trail."
        included={capabilityIncluded(capabilities.data, "audit.agent")}
        tier="agentic"
        platform="audit"
      >
        <div className="space-y-3">
          <FieldSelect
            value={agentEngagementId}
            onChange={(e) => setAgentEngagementId(e.target.value)}
          >
            <option value="">Choose an engagement…</option>
            {(engagements.data ?? []).map((engagement) => (
              <option key={engagement.id} value={engagement.id}>
                {engagement.name} — {engagement.clientName}
              </option>
            ))}
          </FieldSelect>
          <div className="flex gap-2">
            <Textarea
              rows={2}
              value={goal}
              onChange={(e) => setGoal(e.target.value)}
              placeholder="Assess whether the identified risks are consistent with the trial balance."
            />
            <Button
              className="self-end font-semibold"
              disabled={!goal.trim() || !agentEngagementId || runAgent.isPending}
              onClick={() =>
                runAgent.mutate({
                  url: `/api/audit/ai/engagements/${agentEngagementId}/agent`,
                  body: { goal: goal.trim() },
                })
              }
            >
              {runAgent.isPending ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <ShieldCheck className="h-4 w-4" />
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
        <CapabilityGrid platform="audit" enabled={ready} />
      </div>
    </div>
  );
}
