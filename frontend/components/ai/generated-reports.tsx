"use client";

import { useState } from "react";
import {
  Bot,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  FileText,
  Loader2,
  RefreshCw,
  ScanSearch,
  ShieldAlert,
  XCircle,
} from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "sonner";
import { EmptyCard, LoadingRows } from "@/components/workspace";
import { useApiAction, useApiQuery } from "@/hooks/query";
import {
  ProposalCard,
  ToolCard,
  capabilityIncluded,
  capabilityUnlocksOn,
  useAiCapabilities,
  type AiProposal,
} from "@/components/ai/ai-common";

export interface GeneratedReportSection {
  section: string;
  heading: string;
  content: string;
  sources: string[];
}

export interface GeneratedReport {
  id: string;
  engagementId: string;
  capability: string;
  reportScope: string;
  title: string;
  status: "Draft" | "Accepted" | "Rejected";
  provider: string;
  model: string;
  generatedBy: string;
  generatedAt: string;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNote: string | null;
  sections: GeneratedReportSection[];
  disclaimer: string;
}

interface AgenticReportResult {
  report: GeneratedReport;
  steps: { tool: string; arguments: string; result: string }[];
  turnsUsed: number;
  usage?: { unitsConsumed: number; unitsRemaining: number } | null;
}

const statusTone: Record<GeneratedReport["status"], string> = {
  Draft: "bg-warning text-warning-foreground",
  Accepted: "bg-success text-success-foreground",
  Rejected: "bg-muted text-muted-foreground",
};

const fmtWhen = (value: string) =>
  new Date(value).toLocaleString(undefined, {
    dateStyle: "medium",
    timeStyle: "short",
  });

/**
 * One generated draft. Every section shows the engagement records it was written from, so a
 * reviewer can check a claim rather than take it on trust, and the draft cannot leave the
 * awaiting-review state except through the review buttons below.
 */
function ReportCard({
  report,
  engagementId,
  onChanged,
}: {
  report: GeneratedReport;
  engagementId: string;
  onChanged: () => void;
}) {
  const [open, setOpen] = useState(report.status === "Draft");
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [consistency, setConsistency] = useState<AiProposal | null>(null);

  const base = `/api/audit/ai/engagements/${engagementId}/generated-reports/${report.id}`;

  const review = useApiAction<GeneratedReport, { accept: boolean; note: string | null }>({
    onSuccess: (result) => {
      setBusy(null);
      toast.success(
        result.status === "Accepted"
          ? "Draft accepted as a working basis."
          : "Draft rejected.",
      );
      onChanged();
    },
    onError: (errors) => {
      setBusy(null);
      toast.error(errors.join(" "));
    },
  });

  const check = useApiAction<AiProposal>({
    onSuccess: (result) => {
      setBusy(null);
      setConsistency(result);
    },
    onError: (errors) => {
      setBusy(null);
      toast.error(errors.join(" "));
    },
  });

  const regenerate = useApiAction<GeneratedReport, { section: string }>({
    onSuccess: () => {
      setBusy(null);
      toast.success("A new draft was generated with that section rewritten.");
      onChanged();
    },
    onError: (errors) => {
      setBusy(null);
      toast.error(errors.join(" "));
    },
  });

  const pending = busy !== null;

  return (
    <div className="rounded-2xl border border-border/60 bg-card">
      <button
        onClick={() => setOpen((value) => !value)}
        className="flex w-full items-start justify-between gap-3 px-4 py-3 text-left"
      >
        <div className="flex min-w-0 items-start gap-2.5">
          {open ? (
            <ChevronDown className="mt-1 h-4 w-4 flex-shrink-0 text-muted-foreground" />
          ) : (
            <ChevronRight className="mt-1 h-4 w-4 flex-shrink-0 text-muted-foreground" />
          )}
          <div className="min-w-0">
            <div className="truncate text-sm font-semibold">{report.title}</div>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {report.sections.length} section
              {report.sections.length === 1 ? "" : "s"} · {report.provider} ·{" "}
              {report.model} · {fmtWhen(report.generatedAt)}
            </p>
          </div>
        </div>
        <Badge className={`pointer-events-none ${statusTone[report.status]}`}>
          {report.status === "Draft" ? "Awaiting review" : report.status}
        </Badge>
      </button>

      {open && (
        <div className="border-t border-border/60">
          <div className="flex items-start gap-2.5 bg-warning/10 px-4 py-3">
            <ShieldAlert className="mt-0.5 h-4 w-4 flex-shrink-0 text-warning-foreground" />
            <p className="text-xs leading-relaxed">{report.disclaimer}</p>
          </div>

          <div className="divide-y divide-border/60">
            {report.sections.map((section) => (
              <section key={section.section} className="px-4 py-4">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <h4 className="text-sm font-semibold">{section.heading}</h4>
                  {report.status === "Draft" && (
                    <Button
                      size="sm"
                      variant="ghost"
                      className="h-7 text-xs text-muted-foreground"
                      disabled={pending}
                      onClick={() => {
                        setBusy(`regenerate-${section.section}`);
                        regenerate.mutate({
                          url: `${base}/sections`,
                          body: { section: section.section },
                        });
                      }}
                    >
                      {busy === `regenerate-${section.section}` ? (
                        <Loader2 className="mr-1.5 h-3 w-3 animate-spin" />
                      ) : (
                        <RefreshCw className="mr-1.5 h-3 w-3" />
                      )}
                      Regenerate
                    </Button>
                  )}
                </div>
                <div className="mt-1.5 whitespace-pre-wrap text-sm leading-relaxed">
                  {section.content}
                </div>
                {section.sources.length > 0 && (
                  <div className="mt-2.5 flex flex-wrap items-center gap-1.5">
                    <span className="text-[11px] uppercase tracking-wide text-muted-foreground">
                      From
                    </span>
                    {section.sources.map((source) => (
                      <Badge
                        key={source}
                        variant="secondary"
                        className="pointer-events-none text-[11px] font-normal"
                      >
                        {source}
                      </Badge>
                    ))}
                  </div>
                )}
              </section>
            ))}
          </div>

          {consistency && (
            <div className="border-t border-border/60 p-4">
              <ProposalCard proposal={consistency} />
            </div>
          )}

          {report.status === "Draft" ? (
            <div className="space-y-3 border-t border-border/60 p-4">
              <Input
                placeholder="Review note (required to reject)"
                value={note}
                onChange={(event) => setNote(event.target.value)}
              />
              <div className="flex flex-wrap gap-2">
                <Button
                  size="sm"
                  className="font-semibold"
                  disabled={pending}
                  onClick={() => {
                    setBusy("accept");
                    review.mutate({
                      url: `${base}/review`,
                      body: { accept: true, note: note.trim() || null },
                    });
                  }}
                >
                  {busy === "accept" ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                  )}
                  Accept as reviewed
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className="font-semibold"
                  disabled={pending || !note.trim()}
                  onClick={() => {
                    setBusy("reject");
                    review.mutate({
                      url: `${base}/review`,
                      body: { accept: false, note: note.trim() },
                    });
                  }}
                >
                  {busy === "reject" ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <XCircle className="mr-2 h-4 w-4" />
                  )}
                  Reject
                </Button>
                <Button
                  size="sm"
                  variant="ghost"
                  className="text-muted-foreground"
                  disabled={pending}
                  onClick={() => {
                    setBusy("check");
                    check.mutate({ url: `${base}/consistency` });
                  }}
                >
                  {busy === "check" ? (
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  ) : (
                    <ScanSearch className="mr-2 h-4 w-4" />
                  )}
                  Check against the record
                </Button>
              </div>
              <p className="text-xs text-muted-foreground">
                Only an engagement manager or partner can review a draft.
                Accepting records who took responsibility for working from it —
                it does not issue the audit report.
              </p>
            </div>
          ) : (
            <p className="border-t border-border/60 px-4 py-3 text-xs text-muted-foreground">
              {report.status === "Accepted" ? "Accepted" : "Rejected"}{" "}
              {report.reviewedAt ? fmtWhen(report.reviewedAt) : ""}
              {report.reviewNote ? ` — ${report.reviewNote}` : ""}
            </p>
          )}
        </div>
      )}
    </div>
  );
}

/**
 * The AI report-generation surface for one engagement: generate at whatever depth the plan
 * allows, then review what came back. Which generators appear is decided by the server's
 * capability catalogue, never by the client.
 */
export function GeneratedReports({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const capabilities = useAiCapabilities("audit", true);
  const [instruction, setInstruction] = useState("");
  const [running, setRunning] = useState("");

  const reports = useApiQuery<GeneratedReport[]>(
    `/api/audit/ai/engagements/${engagementId}/generated-reports`,
    { queryKey: ["audit-generated-reports", engagementId] },
  );

  const refresh = () =>
    queryClient.invalidateQueries({
      queryKey: ["audit-generated-reports", engagementId],
    });

  const generate = useApiAction<GeneratedReport>({
    onSuccess: () => {
      setRunning("");
      toast.success("A draft was generated and is awaiting review.");
      refresh();
    },
    onError: (errors) => {
      setRunning("");
      toast.error(errors.join(" "));
    },
  });

  const runAgent = useApiAction<AgenticReportResult>({
    onSuccess: (result) => {
      setRunning("");
      toast.success(
        `The agent read the engagement over ${result.turnsUsed} turns; the draft is awaiting review.` +
          (result.usage
            ? ` It used ${result.usage.unitsConsumed} AI credits.`
            : ""),
      );
      refresh();
    },
    onError: (errors) => {
      setRunning("");
      toast.error(errors.join(" "));
    },
  });

  const base = `/api/audit/ai/engagements/${engagementId}`;
  const busy = generate.isPending || runAgent.isPending;
  const included = (key: string) => capabilityIncluded(capabilities.data, key);
  const unlocksOn = (key: string) => capabilityUnlocksOn(capabilities.data, key);

  const body = instruction.trim() ? { instruction: instruction.trim() } : {};

  return (
    <div className="space-y-6">
      <div>
        <Input
          placeholder="Anything the report should emphasise (optional)"
          value={instruction}
          onChange={(event) => setInstruction(event.target.value)}
          className="max-w-xl"
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <ToolCard
          title="Complete draft report"
          description="Every standard section, written from the engagement record."
          included={included("audit.report_draft")}
          tier="advanced"
          platform="audit"
          unlocksOn={unlocksOn("audit.report_draft")}
        >
          <Button
            size="sm"
            className="font-semibold"
            disabled={busy}
            onClick={() => {
              setRunning("draft");
              generate.mutate({ url: `${base}/draft-report`, body });
            }}
          >
            {running === "draft" && busy ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <FileText className="mr-2 h-4 w-4" />
            )}
            Generate draft
          </Button>
        </ToolCard>

        <ToolCard
          title="Full engagement report"
          description="Management and reviewer drafts, with materiality and approach."
          included={included("audit.engagement_report")}
          tier="reasoning"
          platform="audit"
          unlocksOn={unlocksOn("audit.engagement_report")}
        >
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              className="font-semibold"
              disabled={busy}
              onClick={() => {
                setRunning("reviewer");
                generate.mutate({
                  url: `${base}/engagement-report`,
                  body: { ...body, forReviewer: true },
                });
              }}
            >
              {running === "reviewer" && busy && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Reviewer draft
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="font-semibold"
              disabled={busy}
              onClick={() => {
                setRunning("management");
                generate.mutate({
                  url: `${base}/engagement-report`,
                  body: { ...body, forReviewer: false },
                });
              }}
            >
              {running === "management" && busy && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Management draft
            </Button>
          </div>
        </ToolCard>

        <ToolCard
          title="Agentic report generation"
          description="The agent gathers the record itself, drafts, then checks its own draft."
          included={included("audit.agentic_report")}
          tier="agentic"
          platform="audit"
          unlocksOn={unlocksOn("audit.agentic_report")}
        >
          <Button
            size="sm"
            className="font-semibold"
            disabled={busy}
            onClick={() => {
              setRunning("agent");
              runAgent.mutate({ url: `${base}/agentic-report`, body });
            }}
          >
            {running === "agent" && busy ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Bot className="mr-2 h-4 w-4" />
            )}
            Run the workflow
          </Button>
        </ToolCard>
      </div>

      {reports.isLoading ? (
        <LoadingRows count={2} />
      ) : reports.data && reports.data.length > 0 ? (
        <div className="space-y-3">
          {reports.data.map((report) => (
            <ReportCard
              key={report.id}
              report={report}
              engagementId={engagementId}
              onChanged={refresh}
            />
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={FileText}
          title="No AI drafts yet"
          body="Generate a draft above. Every draft stays in review until a manager or partner accepts it — AI never issues the audit report."
        />
      )}
    </div>
  );
}
