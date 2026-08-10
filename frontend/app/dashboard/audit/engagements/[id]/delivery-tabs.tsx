"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  FileCheck2,
  FileSearch,
  FileText,
  History,
  Loader2,
  Plus,
  Upload,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";
import {
  EmptyCard,
  ErrorCard,
  FieldSelect,
  LoadingRows,
  StatusPill,
  fmtBytes,
  fmtDate,
} from "@/components/workspace";
import { useApiAction, useApiQuery, useApiUpload } from "@/hooks/query";
import {
  auditOpinions,
  findingSeverities,
  type ActivityRow,
  type AuditReportView,
  type EvidenceRow,
  type FindingRow,
  type WorkingPaperRow,
} from "@/lib/audit-types";

export function WorkingPapersTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [reference, setReference] = useState("");
  const [title, setTitle] = useState("");
  const [content, setContent] = useState("");

  const papers = useApiQuery<WorkingPaperRow[]>(
    `/api/audit/engagements/${engagementId}/working-papers`,
    { queryKey: ["audit-papers", engagementId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      setOpen(false);
      setReference("");
      setTitle("");
      setContent("");
      queryClient.invalidateQueries({ queryKey: ["audit-papers", engagementId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const nextAction = (paper: WorkingPaperRow) =>
    paper.status === "Draft"
      ? { action: "Prepare", label: "Prepare" }
      : paper.status === "Prepared"
        ? { action: "Review", label: "Review" }
        : paper.status === "Reviewed"
          ? { action: "Approve", label: "Approve" }
          : null;

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          New working paper
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create a working paper</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label>Reference</Label>
              <Input
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                placeholder="B-100"
              />
            </div>
            <div className="space-y-1.5 sm:col-span-2">
              <Label>Title</Label>
              <Input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Cash lead sheet"
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Content</Label>
            <Textarea
              rows={5}
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder="Work performed, results, conclusion…"
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !reference.trim() || !title.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagementId}/working-papers`,
                body: { reference: reference.trim(), title: title.trim(), content },
              })
            }
          >
            Create
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (papers.isLoading) return <LoadingRows />;
  if (papers.isError)
    return <ErrorCard errors={papers.error} onRetry={() => papers.refetch()} />;

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {papers.data && papers.data.length > 0 ? (
        <div className="space-y-3">
          {papers.data.map((paper) => {
            const next = nextAction(paper);
            return (
              <div
                key={paper.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-border/60 bg-card p-4"
              >
                <div>
                  <div className="font-semibold">
                    <span className="mr-2 font-mono text-xs text-muted-foreground">
                      {paper.reference}
                    </span>
                    {paper.title}
                  </div>
                  {paper.openNotes > 0 && (
                    <div className="mt-0.5 text-xs text-amber-600 dark:text-amber-400">
                      {paper.openNotes} open review note
                      {paper.openNotes === 1 ? "" : "s"}
                    </div>
                  )}
                </div>
                <div className="flex items-center gap-2">
                  <StatusPill value={paper.status} />
                  {next && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={action.isPending}
                      onClick={() =>
                        action.mutate({
                          url: `/api/audit/engagements/${engagementId}/working-papers/${paper.id}/sign-off`,
                          body: { action: next.action },
                        })
                      }
                    >
                      {next.label}
                    </Button>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <EmptyCard
          icon={FileCheck2}
          title="No working papers yet"
          body="Working papers document the work performed — with preparer, reviewer and approver sign-offs enforced in order."
          action={addDialog}
        />
      )}
    </div>
  );
}

export function EvidenceTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [description, setDescription] = useState("");

  const evidence = useApiQuery<EvidenceRow[]>(
    `/api/audit/engagements/${engagementId}/evidence`,
    { queryKey: ["audit-evidence", engagementId] },
  );

  const upload = useApiUpload({
    onSuccess: () => {
      toast.success("Evidence uploaded.");
      setFile(null);
      setDescription("");
      queryClient.invalidateQueries({
        queryKey: ["audit-evidence", engagementId],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  if (evidence.isLoading) return <LoadingRows />;
  if (evidence.isError)
    return (
      <ErrorCard errors={evidence.error} onRetry={() => evidence.refetch()} />
    );

  return (
    <div className="space-y-4">
      <Card className="border-border/60">
        <CardHeader className="pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Upload evidence
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="min-w-60 space-y-1.5">
            <Label>File</Label>
            <Input
              type="file"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </div>
          <div className="min-w-60 flex-1 space-y-1.5">
            <Label>Description</Label>
            <Input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Bank confirmation — Main operating account"
            />
          </div>
          <Button
            className="font-semibold"
            disabled={!file || upload.isPending}
            onClick={() => {
              if (!file) return;
              const form = new FormData();
              form.append("file", file);
              form.append("description", description);
              upload.mutate({
                url: `/api/audit/engagements/${engagementId}/evidence`,
                form,
              });
            }}
          >
            {upload.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Upload
          </Button>
        </CardContent>
      </Card>

      {evidence.data && evidence.data.length > 0 ? (
        <div className="space-y-2">
          {evidence.data.map((item) => (
            <div
              key={item.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border/60 bg-card px-4 py-3"
            >
              <div>
                <div className="text-sm font-semibold">{item.fileName}</div>
                <div className="text-xs text-muted-foreground">
                  {item.description || "No description"} ·{" "}
                  {fmtBytes(item.sizeBytes)} · v{item.version} ·{" "}
                  {fmtDate(item.uploadedAt)}
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={FileText}
          title="No evidence yet"
          body="Every file is versioned — superseded, never overwritten — so the trail stays complete."
        />
      )}
    </div>
  );
}

export function FindingsTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [severity, setSeverity] = useState("Medium");
  const [recommendation, setRecommendation] = useState("");

  const findings = useApiQuery<FindingRow[]>(
    `/api/audit/engagements/${engagementId}/findings`,
    { queryKey: ["audit-findings", engagementId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      setOpen(false);
      setTitle("");
      setDescription("");
      queryClient.invalidateQueries({
        queryKey: ["audit-findings", engagementId],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Raise finding
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Raise a finding</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5 sm:col-span-2">
              <Label>Title</Label>
              <Input value={title} onChange={(e) => setTitle(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>Severity</Label>
              <FieldSelect
                value={severity}
                onChange={(e) => setSeverity(e.target.value)}
              >
                {findingSeverities.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </FieldSelect>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Description</Label>
            <Textarea
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>Recommendation</Label>
            <Textarea
              rows={2}
              value={recommendation}
              onChange={(e) => setRecommendation(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !title.trim() || !description.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagementId}/findings`,
                body: {
                  title: title.trim(),
                  description,
                  severity,
                  recommendation,
                  evidenceIds: [],
                },
              })
            }
          >
            Raise finding
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (findings.isLoading) return <LoadingRows />;
  if (findings.isError)
    return (
      <ErrorCard errors={findings.error} onRetry={() => findings.refetch()} />
    );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {findings.data && findings.data.length > 0 ? (
        <div className="space-y-3">
          {findings.data.map((finding) => (
            <div
              key={finding.id}
              className="rounded-xl border border-border/60 bg-card p-4"
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="font-semibold">{finding.title}</div>
                <div className="flex items-center gap-2">
                  <StatusPill value={finding.severity} />
                  <StatusPill value={finding.status} />
                  {finding.status === "Open" && (
                    <Button
                      size="sm"
                      variant="outline"
                      disabled={action.isPending}
                      onClick={() =>
                        action.mutate({
                          url: `/api/audit/engagements/${engagementId}/findings/${finding.id}/status`,
                          body: { action: "Resolve", note: "Resolved" },
                        })
                      }
                    >
                      Resolve
                    </Button>
                  )}
                </div>
              </div>
              <p className="mt-1.5 text-sm text-muted-foreground">
                {finding.description}
              </p>
              {finding.recommendation && (
                <p className="mt-2 text-xs text-muted-foreground">
                  <span className="font-medium text-foreground">
                    Recommendation:
                  </span>{" "}
                  {finding.recommendation}
                </p>
              )}
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={FileSearch}
          title="No findings raised"
          body="Findings capture what the audit surfaced, with severity, recommendation and resolution tracking."
          action={addDialog}
        />
      )}
    </div>
  );
}

export function ReportTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const report = useApiQuery<AuditReportView>(
    `/api/audit/engagements/${engagementId}/report`,
    { queryKey: ["audit-report", engagementId], retry: false },
  );

  const [opinion, setOpinion] = useState("Unqualified");
  const [basis, setBasis] = useState("");
  const [matters, setMatters] = useState("");

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      queryClient.invalidateQueries({ queryKey: ["audit-report", engagementId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const current = report.data;

  return (
    <div className="space-y-4">
      {current && (
        <div className="rounded-xl border border-border/60 bg-card p-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div className="font-semibold">
              Current draft: {current.opinion} opinion
            </div>
            {current.isFinalized ? (
              <StatusPill value="Completed" />
            ) : (
              <Button
                size="sm"
                variant="outline"
                disabled={action.isPending}
                onClick={() =>
                  action.mutate({
                    url: `/api/audit/engagements/${engagementId}/report/finalize`,
                  })
                }
              >
                Finalize report
              </Button>
            )}
          </div>
          {current.basisForOpinion && (
            <p className="mt-2 text-sm text-muted-foreground">
              {current.basisForOpinion}
            </p>
          )}
        </div>
      )}

      {!current?.isFinalized && (
        <Card className="border-border/60">
          <CardHeader className="pb-3">
            <CardTitle className="font-display text-base font-semibold">
              {current ? "Update the draft report" : "Draft the audit report"}
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-1.5">
              <Label>Opinion</Label>
              <FieldSelect
                value={opinion}
                onChange={(e) => setOpinion(e.target.value)}
              >
                {auditOpinions.map((value) => (
                  <option key={value}>{value}</option>
                ))}
              </FieldSelect>
            </div>
            <div className="space-y-1.5">
              <Label>Basis for opinion</Label>
              <Textarea
                rows={3}
                value={basis}
                onChange={(e) => setBasis(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Key audit matters</Label>
              <Textarea
                rows={3}
                value={matters}
                onChange={(e) => setMatters(e.target.value)}
              />
            </div>
            <Button
              className="font-semibold"
              disabled={action.isPending || !basis.trim()}
              onClick={() =>
                action.mutate({
                  url: `/api/audit/engagements/${engagementId}/report`,
                  method: "put",
                  body: {
                    opinion,
                    basisForOpinion: basis,
                    keyAuditMatters: matters,
                    otherInformation: "",
                  },
                })
              }
            >
              Save draft
            </Button>
          </CardContent>
        </Card>
      )}
    </div>
  );
}

export function ActivityTab({ engagementId }: { engagementId: string }) {
  const activity = useApiQuery<ActivityRow[]>(
    `/api/audit/engagements/${engagementId}/activity`,
    { queryKey: ["audit-activity", engagementId] },
  );

  if (activity.isLoading) return <LoadingRows />;
  if (activity.isError)
    return (
      <ErrorCard errors={activity.error} onRetry={() => activity.refetch()} />
    );

  return activity.data && activity.data.length > 0 ? (
    <div className="space-y-2">
      {activity.data.map((entry) => (
        <div
          key={entry.id}
          className="flex items-start gap-3 rounded-xl border border-border/60 bg-card px-4 py-3"
        >
          <History className="mt-0.5 h-4 w-4 flex-shrink-0 text-muted-foreground" />
          <div className="min-w-0 flex-1">
            <p className="text-sm">{entry.summary}</p>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {entry.actorEmail} · {new Date(entry.occurredAt).toLocaleString()}
            </p>
          </div>
        </div>
      ))}
    </div>
  ) : (
    <EmptyCard
      icon={History}
      title="No activity yet"
      body="Every material change to this engagement is recorded here, append-only."
    />
  );
}
