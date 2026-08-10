"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import { FieldSelect } from "@/components/workspace";
import {
  AgentReportCard,
  AiRunButton,
  ProposalCard,
  ToolCard,
  capabilityIncluded,
  useAiCapabilities,
  type AgentReport,
  type AiProposal,
} from "@/components/ai/ai-common";
import { useApiAction, useApiQuery } from "@/hooks/query";
import type { WorkingPaperRow } from "@/lib/audit-types";

export function AiTab({ engagementId }: { engagementId: string }) {
  const capabilities = useAiCapabilities("audit", true);
  const papers = useApiQuery<WorkingPaperRow[]>(
    `/api/audit/engagements/${engagementId}/working-papers`,
    { queryKey: ["audit-papers", engagementId] },
  );

  const [focusArea, setFocusArea] = useState("");
  const [paperTopic, setPaperTopic] = useState("");
  const [observation, setObservation] = useState("");
  const [summarizePaperId, setSummarizePaperId] = useState("");
  const [goal, setGoal] = useState("");
  const [proposal, setProposal] = useState<AiProposal | null>(null);
  const [report, setReport] = useState<AgentReport | null>(null);
  const [runningTool, setRunningTool] = useState("");

  const run = useApiAction<AiProposal>({
    onSuccess: (result) => {
      setProposal(result);
      setReport(null);
      setRunningTool("");
    },
    onError: (errors) => {
      toast.error(errors.join(" "));
      setRunningTool("");
    },
  });

  const runAgent = useApiAction<AgentReport>({
    onSuccess: (result) => {
      setReport(result);
      setProposal(null);
      setRunningTool("");
    },
    onError: (errors) => {
      toast.error(errors.join(" "));
      setRunningTool("");
    },
  });

  const base = `/api/audit/ai/engagements/${engagementId}`;
  const busy = run.isPending || runAgent.isPending;

  const fire = (tool: string, url: string, body?: object) => {
    setRunningTool(tool);
    run.mutate({ url, body: body ?? {} });
  };

  const included = (key: string) => capabilityIncluded(capabilities.data, key);

  return (
    <div className="space-y-6">
      <div className="grid gap-4 lg:grid-cols-2">
        <ToolCard
          title="Summarize a working paper"
          description="A reviewer-ready summary: purpose, contents, conclusions and loose ends."
          included={included("audit.document_summary")}
          tier="basic"
          platform="audit"
        >
          <div className="flex gap-2">
            <FieldSelect
              className="flex-1"
              value={summarizePaperId}
              onChange={(e) => setSummarizePaperId(e.target.value)}
            >
              <option value="">Choose a working paper…</option>
              {(papers.data ?? []).map((paper) => (
                <option key={paper.id} value={paper.id}>
                  {paper.reference} — {paper.title}
                </option>
              ))}
            </FieldSelect>
            <AiRunButton
              busy={busy}
              spinning={runningTool === "summarize" && busy}
              label="Summarize"
              disabled={!summarizePaperId}
              onClick={() =>
                fire("summarize", `${base}/summarize`, {
                  workingPaperId: summarizePaperId,
                })
              }
            />
          </div>
        </ToolCard>

        <ToolCard
          title="Suggest risks"
          description="Risks of material misstatement this engagement may be missing."
          included={included("audit.risk_suggestions")}
          tier="advanced"
          platform="audit"
        >
          <div className="flex gap-2">
            <Input
              placeholder="Focus area (optional), e.g. Revenue"
              value={focusArea}
              onChange={(e) => setFocusArea(e.target.value)}
            />
            <AiRunButton
              busy={busy}
              spinning={runningTool === "suggest-risks" && busy}
              label="Suggest"
              onClick={() =>
                fire("suggest-risks", `${base}/suggest-risks`, {
                  focusArea: focusArea || null,
                })
              }
            />
          </div>
        </ToolCard>

        <ToolCard
          title="Suggest procedures"
          description="Procedures responsive to the risks already identified."
          included={included("audit.procedure_suggestions")}
          tier="advanced"
          platform="audit"
        >
          <AiRunButton
            busy={busy}
            spinning={runningTool === "suggest-procedures" && busy}
            label="Suggest"
            onClick={() => fire("suggest-procedures", `${base}/suggest-procedures`)}
          />
        </ToolCard>

        <ToolCard
          title="Draft a working paper"
          description="A structured draft your team refines and signs off."
          included={included("audit.working_paper_draft")}
          tier="advanced"
          platform="audit"
        >
          <div className="flex gap-2">
            <Input
              placeholder="Topic, e.g. Cash lead sheet"
              value={paperTopic}
              onChange={(e) => setPaperTopic(e.target.value)}
            />
            <AiRunButton
              busy={busy}
              spinning={runningTool === "draft-paper" && busy}
              label="Draft"
              disabled={!paperTopic.trim()}
              onClick={() =>
                fire("draft-paper", `${base}/draft-working-paper`, {
                  topic: paperTopic.trim(),
                })
              }
            />
          </div>
        </ToolCard>

        <ToolCard
          title="Draft a finding"
          description="Turn an observation into a structured finding with a recommendation."
          included={included("audit.finding_draft")}
          tier="advanced"
          platform="audit"
        >
          <div className="flex gap-2">
            <Input
              placeholder="What did you observe?"
              value={observation}
              onChange={(e) => setObservation(e.target.value)}
            />
            <AiRunButton
              busy={busy}
              spinning={runningTool === "draft-finding" && busy}
              label="Draft"
              disabled={!observation.trim()}
              onClick={() =>
                fire("draft-finding", `${base}/draft-finding`, {
                  observation: observation.trim(),
                })
              }
            />
          </div>
        </ToolCard>

        <ToolCard
          title="Cross-document risk analysis"
          description="Are the risks complete, correctly rated and adequately answered?"
          included={included("audit.risk_analysis")}
          tier="reasoning"
          platform="audit"
        >
          <AiRunButton
            busy={busy}
            spinning={runningTool === "analyze-risks" && busy}
            label="Analyze"
            onClick={() => fire("analyze-risks", `${base}/analyze-risks`)}
          />
        </ToolCard>

        <ToolCard
          title="Trial balance anomaly detection"
          description="Unusual balances, signs and patterns worth investigating."
          included={included("audit.anomaly_detection")}
          tier="reasoning"
          platform="audit"
        >
          <AiRunButton
            busy={busy}
            spinning={runningTool === "detect-anomalies" && busy}
            label="Detect"
            onClick={() => fire("detect-anomalies", `${base}/detect-anomalies`)}
          />
        </ToolCard>

        <ToolCard
          title="Review assistance"
          description="A reviewing partner's checklist: what is incomplete before sign-off."
          included={included("audit.review_assistance")}
          tier="reasoning"
          platform="audit"
        >
          <AiRunButton
            busy={busy}
            spinning={runningTool === "assist-review" && busy}
            label="Review"
            onClick={() => fire("assist-review", `${base}/assist-review`)}
          />
        </ToolCard>

        <ToolCard
          title="Draft the audit report"
          description="Opinion, basis and key audit matters — partner judgments flagged."
          included={included("audit.report_draft")}
          tier="reasoning"
          platform="audit"
        >
          <AiRunButton
            busy={busy}
            spinning={runningTool === "draft-report" && busy}
            label="Draft"
            onClick={() => fire("draft-report", `${base}/draft-report`)}
          />
        </ToolCard>

        <ToolCard
          title="Agent investigation"
          description="Multi-step investigation across this engagement's records, with a full tool trail."
          included={included("audit.agent")}
          tier="agentic"
          platform="audit"
        >
          <div className="flex gap-2">
            <Textarea
              rows={2}
              placeholder="What should the agent investigate?"
              value={goal}
              onChange={(e) => setGoal(e.target.value)}
            />
            <Button
              size="sm"
              className="self-end font-semibold"
              disabled={!goal.trim() || busy}
              onClick={() => {
                setRunningTool("agent");
                runAgent.mutate({
                  url: `${base}/agent`,
                  body: { goal: goal.trim() },
                });
              }}
            >
              {runningTool === "agent" && busy ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                "Run"
              )}
            </Button>
          </div>
        </ToolCard>
      </div>

      {proposal && <ProposalCard proposal={proposal} />}
      {report && <AgentReportCard report={report} />}
    </div>
  );
}
