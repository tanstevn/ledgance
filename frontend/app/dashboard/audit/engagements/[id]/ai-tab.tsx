"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "sonner";
import { SelectField } from "@/components/workspace";
import {
  AgentReportCard,
  AiCreditsStrip,
  AiRunButton,
  ProposalCard,
  ToolCard,
  capabilityIncluded,
  capabilityUnlocksOn,
  useAiCapabilities,
  type AgentReport,
  type AiProposal,
} from "@/components/ai/ai-common";
import { GeneratedReports } from "@/components/ai/generated-reports";
import { useApiAction, useApiQuery } from "@/hooks/query";
import type { WorkingPaperRow } from "@/lib/audit-types";

const reportSections = [
  "ExecutiveSummary",
  "Scope",
  "Approach",
  "Materiality",
  "RiskAssessment",
  "ProceduresPerformed",
  "EvidenceSummary",
  "Findings",
  "Recommendations",
  "BasisForOpinion",
  "KeyAuditMatters",
  "ManagementSummary",
  "Conclusion",
];

/** "RiskAssessment" reads as "Risk assessment" in a dropdown. */
const sectionLabel = (value: string) =>
  value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/^./, (character) => character.toUpperCase())
    .toLowerCase()
    .replace(/^./, (character) => character.toUpperCase());

export function AiTab({ engagementId }: { engagementId: string }) {
  const capabilities = useAiCapabilities("audit", true);
  const papers = useApiQuery<WorkingPaperRow[]>(
    `/api/audit/engagements/${engagementId}/working-papers`,
    { queryKey: ["audit-papers", engagementId] },
  );

  const [focusArea, setFocusArea] = useState("");
  const [paperTopic, setPaperTopic] = useState("");
  const [observation, setObservation] = useState("");
  const [note, setNote] = useState("");
  const [wording, setWording] = useState("");
  const [question, setQuestion] = useState("");
  const [section, setSection] = useState("ExecutiveSummary");
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
  const unlocksOn = (key: string) => capabilityUnlocksOn(capabilities.data, key);

  return (
    <Tabs defaultValue="assist" className="space-y-6">
      <AiCreditsStrip />

      <TabsList>
        <TabsTrigger value="assist">Assist</TabsTrigger>
        <TabsTrigger value="analyze">Analyze</TabsTrigger>
        <TabsTrigger value="reports">Reports</TabsTrigger>
      </TabsList>

      <TabsContent value="assist" className="space-y-6">
        <div className="grid gap-4 lg:grid-cols-2">
          <ToolCard
            title="Summarize the findings"
            description="Plain-language summaries of what was found and what it means."
            included={included("audit.finding_summary")}
            tier="basic"
            platform="audit"
            unlocksOn={unlocksOn("audit.finding_summary")}
          >
            <AiRunButton
              busy={busy}
              spinning={runningTool === "summarize-findings" && busy}
              label="Summarize"
              onClick={() =>
                fire("summarize-findings", `${base}/summarize-findings`)
              }
            />
          </ToolCard>

          <ToolCard
            title="Summarize the engagement"
            description="Where it stands, what has been found and what is still open."
            included={included("audit.engagement_summary")}
            tier="basic"
            platform="audit"
            unlocksOn={unlocksOn("audit.engagement_summary")}
          >
            <AiRunButton
              busy={busy}
              spinning={runningTool === "summarize-engagement" && busy}
              label="Summarize"
              onClick={() =>
                fire("summarize-engagement", `${base}/summarize-engagement`)
              }
            />
          </ToolCard>

          <ToolCard
            title="Summarize a working paper"
            description="A reviewer-ready summary: purpose, contents, conclusions and loose ends."
            included={included("audit.document_summary")}
            tier="basic"
            platform="audit"
            unlocksOn={unlocksOn("audit.document_summary")}
          >
            <div className="flex gap-2">
              <SelectField
                className="flex-1"
                value={summarizePaperId}
                onValueChange={setSummarizePaperId}
                placeholder="Choose a working paper…"
                options={(papers.data ?? []).map((paper) => ({
                  value: paper.id,
                  label: `${paper.reference} — ${paper.title}`,
                }))}
              />
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
            title="Write up a note"
            description="Turn a rough observation into a clean engagement note."
            included={included("audit.note_draft")}
            tier="basic"
            platform="audit"
            unlocksOn={unlocksOn("audit.note_draft")}
          >
            <div className="flex gap-2">
              <Input
                placeholder="What did you observe?"
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
              <AiRunButton
                busy={busy}
                spinning={runningTool === "draft-note" && busy}
                label="Write up"
                disabled={!note.trim()}
                onClick={() =>
                  fire("draft-note", `${base}/draft-note`, {
                    observation: note.trim(),
                  })
                }
              />
            </div>
          </ToolCard>

          <ToolCard
            title="Improve working-paper wording"
            description="Rewrites your text for clarity. It changes wording, never conclusions."
            included={included("audit.wording_assistance")}
            tier="basic"
            platform="audit"
            unlocksOn={unlocksOn("audit.wording_assistance")}
          >
            <div className="flex gap-2">
              <Textarea
                rows={2}
                placeholder="Paste the passage to tidy up"
                value={wording}
                onChange={(e) => setWording(e.target.value)}
              />
              <AiRunButton
                busy={busy}
                spinning={runningTool === "improve-wording" && busy}
                label="Rewrite"
                disabled={!wording.trim()}
                onClick={() =>
                  fire("improve-wording", `${base}/improve-wording`, {
                    text: wording.trim(),
                  })
                }
              />
            </div>
          </ToolCard>

          <ToolCard
            title="Audit planning assistance"
            description="Scope, objectives and strategy proposed from the engagement record."
            included={included("audit.plan_assistance")}
            tier="advanced"
            platform="audit"
            unlocksOn={unlocksOn("audit.plan_assistance")}
          >
            <div className="flex gap-2">
              <Input
                placeholder="Focus area (optional)"
                value={focusArea}
                onChange={(e) => setFocusArea(e.target.value)}
              />
              <AiRunButton
                busy={busy}
                spinning={runningTool === "assist-plan" && busy}
                label="Plan"
                onClick={() =>
                  fire("assist-plan", `${base}/assist-plan`, {
                    focusArea: focusArea || null,
                  })
                }
              />
            </div>
          </ToolCard>

          <ToolCard
            title="Materiality assistance"
            description="Benchmarks and thresholds discussed — the figure stays the partner's call."
            included={included("audit.materiality_assistance")}
            tier="advanced"
            platform="audit"
            unlocksOn={unlocksOn("audit.materiality_assistance")}
          >
            <AiRunButton
              busy={busy}
              spinning={runningTool === "assist-materiality" && busy}
              label="Discuss"
              onClick={() =>
                fire("assist-materiality", `${base}/assist-materiality`)
              }
            />
          </ToolCard>

          <ToolCard
            title="Suggest risks"
            description="Risks of material misstatement this engagement may be missing."
            included={included("audit.risk_suggestions")}
            tier="advanced"
            platform="audit"
            unlocksOn={unlocksOn("audit.risk_suggestions")}
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
            unlocksOn={unlocksOn("audit.procedure_suggestions")}
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
            unlocksOn={unlocksOn("audit.working_paper_draft")}
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
            unlocksOn={unlocksOn("audit.finding_draft")}
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
            title="Draft one report section"
            description="A single section, written from the engagement record and yours to edit."
            included={included("audit.report_section")}
            tier="advanced"
            platform="audit"
            unlocksOn={unlocksOn("audit.report_section")}
          >
            <div className="flex gap-2">
              <SelectField
                className="flex-1"
                value={section}
                onValueChange={setSection}
                options={reportSections.map((value) => ({
                  value,
                  label: sectionLabel(value),
                }))}
              />
              <AiRunButton
                busy={busy}
                spinning={runningTool === "report-section" && busy}
                label="Draft"
                onClick={() =>
                  fire("report-section", `${base}/report-section`, { section })
                }
              />
            </div>
          </ToolCard>
        </div>

        {proposal && <ProposalCard proposal={proposal} />}
        {report && <AgentReportCard report={report} />}
      </TabsContent>

      <TabsContent value="analyze" className="space-y-6">
        <div className="grid gap-4 lg:grid-cols-2">
          <ToolCard
            title="Engagement intelligence"
            description="One answer across the whole record — risks, procedures, evidence, findings."
            included={included("audit.engagement_intelligence")}
            tier="advanced"
            platform="audit"
            unlocksOn={unlocksOn("audit.engagement_intelligence")}
          >
            <div className="flex gap-2">
              <Input
                placeholder="Ask about this engagement (optional)"
                value={question}
                onChange={(e) => setQuestion(e.target.value)}
              />
              <AiRunButton
                busy={busy}
                spinning={runningTool === "analyze-engagement" && busy}
                label="Analyze"
                onClick={() =>
                  fire("analyze-engagement", `${base}/analyze-engagement`, {
                    question: question.trim() || null,
                  })
                }
              />
            </div>
          </ToolCard>

          <ToolCard
            title="Evidence gap analysis"
            description="Which risks and procedures are supported by evidence — and which are not."
            included={included("audit.evidence_analysis")}
            tier="advanced"
            platform="audit"
            unlocksOn={unlocksOn("audit.evidence_analysis")}
          >
            <AiRunButton
              busy={busy}
              spinning={runningTool === "analyze-evidence" && busy}
              label="Analyze"
              onClick={() => fire("analyze-evidence", `${base}/analyze-evidence`)}
            />
          </ToolCard>

          <ToolCard
            title="Cross-document risk analysis"
            description="Are the risks complete, correctly rated and adequately answered?"
            included={included("audit.risk_analysis")}
            tier="reasoning"
            platform="audit"
            unlocksOn={unlocksOn("audit.risk_analysis")}
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
            unlocksOn={unlocksOn("audit.anomaly_detection")}
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
            unlocksOn={unlocksOn("audit.review_assistance")}
          >
            <AiRunButton
              busy={busy}
              spinning={runningTool === "assist-review" && busy}
              label="Review"
              onClick={() => fire("assist-review", `${base}/assist-review`)}
            />
          </ToolCard>

          <ToolCard
            title="Agent investigation"
            description="Multi-step investigation across this engagement's records, with a full tool trail."
            included={included("audit.agent")}
            tier="agentic"
            platform="audit"
            unlocksOn={unlocksOn("audit.agent")}
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
      </TabsContent>

      <TabsContent value="reports">
        <GeneratedReports engagementId={engagementId} />
      </TabsContent>
    </Tabs>
  );
}
