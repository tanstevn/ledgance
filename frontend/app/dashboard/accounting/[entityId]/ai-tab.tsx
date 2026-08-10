"use client";

import { useState } from "react";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
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
import type { FiscalPeriodRow } from "@/lib/accounting-types";

export function AiTab({ entityId }: { entityId: string }) {
  const capabilities = useAiCapabilities("accounting", true);
  const periods = useApiQuery<FiscalPeriodRow[]>(
    `/api/accounting/entities/${entityId}/periods`,
    { queryKey: ["acc-periods", entityId] },
  );

  const [periodId, setPeriodId] = useState("");
  const [comparePeriodId, setComparePeriodId] = useState("");
  const [transaction, setTransaction] = useState("");
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

  const base = `/api/accounting/ai/entities/${entityId}`;
  const busy = run.isPending || runAgent.isPending;

  const fire = (tool: string, url: string, body?: object) => {
    setRunningTool(tool);
    run.mutate({ url, body: body ?? {} });
  };

  const included = (key: string) => capabilityIncluded(capabilities.data, key);

  const periodTools: {
    tool: string;
    endpoint: string;
    title: string;
    description: string;
    key: string;
    tier: string;
  }[] = [
    {
      tool: "summarize",
      endpoint: "summarize",
      title: "Period financial summary",
      description: "What was earned, spent and where the period landed — in plain language.",
      key: "accounting.financial_summary",
      tier: "basic",
    },
    {
      tool: "explain-statements",
      endpoint: "explain-statements",
      title: "Explain the statements",
      description: "The income statement and balance sheet, explained for a non-specialist.",
      key: "accounting.statement_explanation",
      tier: "advanced",
    },
    {
      tool: "detect-anomalies",
      endpoint: "detect-anomalies",
      title: "Detect anomalies",
      description: "Unusual balances, patterns and entries worth a closer look.",
      key: "accounting.anomaly_detection",
      tier: "reasoning",
    },
    {
      tool: "analyze-financials",
      endpoint: "analyze-financials",
      title: "Financial analysis",
      description: "Profitability, liquidity and leverage as of the period end.",
      key: "accounting.financial_analysis",
      tier: "reasoning",
    },
    {
      tool: "assist-close",
      endpoint: "assist-close",
      title: "Period-close review",
      description: "What blocks the close and what to verify first.",
      key: "accounting.close_assistance",
      tier: "reasoning",
    },
  ];

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-56 space-y-1.5">
          <Label>Fiscal period for period-based tools</Label>
          <FieldSelect
            value={periodId}
            onChange={(e) => setPeriodId(e.target.value)}
          >
            <option value="">Select a period…</option>
            {(periods.data ?? []).map((period) => (
              <option key={period.id} value={period.id}>
                {period.name}
              </option>
            ))}
          </FieldSelect>
        </div>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        {periodTools.map((item) => (
          <ToolCard
            key={item.tool}
            title={item.title}
            description={item.description}
            included={included(item.key)}
            tier={item.tier}
            platform="accounting"
          >
            <AiRunButton
              busy={busy}
              spinning={runningTool === item.tool && busy}
              label="Generate"
              disabled={!periodId}
              onClick={() =>
                fire(item.tool, `${base}/periods/${periodId}/${item.endpoint}`)
              }
            />
          </ToolCard>
        ))}

        <ToolCard
          title="Suggest a journal entry"
          description="Describe the transaction; get a balanced entry proposal from your chart of accounts."
          included={included("accounting.entry_suggestion")}
          tier="advanced"
          platform="accounting"
        >
          <div className="flex gap-2">
            <Textarea
              rows={2}
              placeholder="Paid 5,000 office rent for March by bank transfer"
              value={transaction}
              onChange={(e) => setTransaction(e.target.value)}
            />
            <Button
              size="sm"
              className="self-end font-semibold"
              disabled={!transaction.trim() || busy}
              onClick={() =>
                fire("suggest-entry", `${base}/suggest-entry`, {
                  transactionDescription: transaction.trim(),
                })
              }
            >
              {runningTool === "suggest-entry" && busy ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                "Suggest"
              )}
            </Button>
          </div>
        </ToolCard>

        <ToolCard
          title="Variance analysis"
          description="Compare this period against another and explain what moved."
          included={included("accounting.variance_analysis")}
          tier="advanced"
          platform="accounting"
        >
          <div className="flex gap-2">
            <FieldSelect
              className="flex-1"
              value={comparePeriodId}
              onChange={(e) => setComparePeriodId(e.target.value)}
            >
              <option value="">Compare against…</option>
              {(periods.data ?? [])
                .filter((period) => period.id !== periodId)
                .map((period) => (
                  <option key={period.id} value={period.id}>
                    {period.name}
                  </option>
                ))}
            </FieldSelect>
            <AiRunButton
              busy={busy}
              spinning={runningTool === "variance" && busy}
              label="Compare"
              disabled={!periodId || !comparePeriodId}
              onClick={() =>
                fire("variance", `${base}/analyze-variance`, {
                  basePeriodId: periodId,
                  comparePeriodId,
                })
              }
            />
          </div>
        </ToolCard>

        <ToolCard
          title="Agent investigation"
          description="Multi-step investigation across these books, with a full tool trail."
          included={included("accounting.agent")}
          tier="agentic"
          platform="accounting"
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
