"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Loader2,
  Trash2,
  UserPlus,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { toast } from "sonner";
import {
  FieldSelect,
  ProgressTrack,
  StatusPill,
  fmtDate,
  fmtMoney,
} from "@/components/workspace";
import { useApiAction, useApiQuery } from "@/hooks/query";
import {
  engagementRoles,
  type EngagementDetail,
  type OrganizationMemberRow,
  type WorkingPaperRow,
} from "@/lib/audit-types";

export interface EngagementGate {
  label: string;
  cleared: boolean;
  outstanding: number;
  tab: string;
}

/**
 * The stage gates the Engagement aggregate itself checks before a status transition — the
 * overview and the header band read from this one list so they cannot disagree.
 */
export function engagementGates(engagement: EngagementDetail): EngagementGate[] {
  const progress = engagement.progress;

  return [
    {
      label: "Procedures closed",
      outstanding: progress.openProcedures,
      cleared: progress.openProcedures === 0,
      tab: "procedures",
    },
    {
      label: "Working papers approved",
      outstanding: progress.unapprovedWorkingPapers,
      cleared: progress.unapprovedWorkingPapers === 0,
      tab: "papers",
    },
    {
      label: "Review notes resolved",
      outstanding: progress.openReviewNotes,
      cleared: progress.openReviewNotes === 0,
      tab: "papers",
    },
    {
      label: "Findings resolved",
      outstanding: progress.openFindings,
      cleared: progress.openFindings === 0,
      tab: "findings",
    },
    {
      label: "High risks addressed",
      outstanding: progress.unaddressedHighRisks,
      cleared: progress.unaddressedHighRisks === 0,
      tab: "risks",
    },
    {
      label: "Report finalized",
      outstanding: progress.reportFinalized ? 0 : 1,
      cleared: progress.reportFinalized,
      tab: "report",
    },
  ];
}

const signOffStates = ["Approved", "Reviewed", "Prepared", "Draft"] as const;

function SignOffStatus({ papers }: { papers: WorkingPaperRow[] }) {
  const counts = signOffStates.map((state) => ({
    state,
    count: papers.filter((paper) => paper.status === state).length,
  }));

  const max = Math.max(1, ...counts.map((entry) => entry.count));

  return (
    <Card className="border-border/60">
      <CardHeader className="pb-3">
        <CardTitle className="font-display text-base font-semibold">
          Working paper sign-off status
        </CardTitle>
      </CardHeader>
      <CardContent>
        {papers.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No working papers yet. They appear here as the team prepares, reviews and
            approves them.
          </p>
        ) : (
          <div className="space-y-3">
            {counts.map(({ state, count }) => (
              <div key={state} className="flex items-center gap-3">
                <span className="w-24 shrink-0">
                  <StatusPill value={state} />
                </span>
                <ProgressTrack
                  value={count}
                  max={max}
                  tone={state === "Approved" ? "bg-success" : "bg-primary"}
                />
                <span className="w-6 shrink-0 text-right text-sm font-semibold">
                  {count}
                </span>
              </div>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
  );
}

function NeedsAttention({
  engagement,
  onNavigate,
}: {
  engagement: EngagementDetail;
  onNavigate: (tab: string) => void;
}) {
  const gates = engagementGates(engagement);
  const outstanding = gates.filter((gate) => !gate.cleared);

  return (
    <Card className="border-border/60">
      <CardHeader className="pb-3">
        <CardTitle className="font-display text-base font-semibold">
          Needs attention
        </CardTitle>
      </CardHeader>
      <CardContent className="space-y-1.5">
        {!engagement.plan?.isApproved && (
          <button
            type="button"
            onClick={() => onNavigate("planning")}
            className="flex w-full items-center justify-between rounded-xl border border-border/60 px-3 py-2.5 text-left text-sm transition-colors hover:border-primary/40"
          >
            <span className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-warning" />
              {engagement.plan ? "Audit plan not approved" : "Audit plan not written"}
            </span>
            <ArrowRight className="h-4 w-4 text-muted-foreground" />
          </button>
        )}

        {outstanding.map((gate) => (
          <button
            key={gate.label}
            type="button"
            onClick={() => onNavigate(gate.tab)}
            className="flex w-full items-center justify-between rounded-xl border border-border/60 px-3 py-2.5 text-left text-sm transition-colors hover:border-primary/40"
          >
            <span className="flex items-center gap-2">
              <AlertTriangle className="h-4 w-4 text-warning" />
              {gate.label}
            </span>
            <span className="flex items-center gap-2 text-xs text-muted-foreground">
              {gate.label === "Report finalized" ? "pending" : `${gate.outstanding} open`}
              <ArrowRight className="h-4 w-4" />
            </span>
          </button>
        ))}

        {outstanding.length === 0 && engagement.plan?.isApproved && (
          <p className="flex items-center gap-2 rounded-xl border border-border/60 px-3 py-2.5 text-sm text-success">
            <CheckCircle2 className="h-4 w-4" />
            Every stage gate is clear — this engagement is ready to sign off.
          </p>
        )}
      </CardContent>
    </Card>
  );
}

/** The overview per the product design: sign-off state on the left, what's blocking on the right. */
export function OverviewTab({
  engagement,
  papers,
  onNavigate,
}: {
  engagement: EngagementDetail;
  papers: WorkingPaperRow[];
  onNavigate: (tab: string) => void;
}) {
  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <SignOffStatus papers={papers} />
      <NeedsAttention engagement={engagement} onNavigate={onNavigate} />
    </div>
  );
}

/** Plan, materiality and the engagement facts — the editing surfaces the overview used to host. */
export function PlanningTab({
  engagement,
  onChanged,
}: {
  engagement: EngagementDetail;
  onChanged: () => void;
}) {
  const [scope, setScope] = useState(engagement.plan?.scope ?? "");
  const [objectives, setObjectives] = useState(engagement.plan?.objectives ?? "");
  const [strategy, setStrategy] = useState(engagement.plan?.strategy ?? "");
  const [overall, setOverall] = useState(
    engagement.materiality?.overallAmount?.toString() ?? "",
  );
  const [performance, setPerformance] = useState(
    engagement.materiality?.performanceAmount?.toString() ?? "",
  );
  const [trivial, setTrivial] = useState(
    engagement.materiality?.clearlyTrivialThreshold?.toString() ?? "",
  );
  const [basis, setBasis] = useState(engagement.materiality?.basis ?? "");
  const [rationale, setRationale] = useState(
    engagement.materiality?.rationale ?? "",
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      onChanged();
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card className="border-border/60">
        <CardHeader className="pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Engagement
          </CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          <div className="flex justify-between">
            <span className="text-muted-foreground">Period</span>
            <span>
              {fmtDate(engagement.periodStart)} – {fmtDate(engagement.periodEnd)}
            </span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Fiscal year end</span>
            <span>{fmtDate(engagement.fiscalYearEnd)}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Budget</span>
            <span>{engagement.budgetHours} hours</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Team size</span>
            <span>{engagement.team.length}</span>
          </div>
          <div className="flex justify-between">
            <span className="text-muted-foreground">Created</span>
            <span>{fmtDate(engagement.createdAt)}</span>
          </div>
        </CardContent>
      </Card>

      <Card className="border-border/60">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Materiality
          </CardTitle>
          {engagement.materiality && (
            <span className="text-xs text-muted-foreground">
              Basis: {engagement.materiality.basis || "—"}
            </span>
          )}
        </CardHeader>
        <CardContent>
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label className="text-xs">Overall</Label>
              <Input
                type="number"
                value={overall}
                onChange={(e) => setOverall(e.target.value)}
                placeholder="100000"
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Performance</Label>
              <Input
                type="number"
                value={performance}
                onChange={(e) => setPerformance(e.target.value)}
                placeholder="75000"
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Clearly trivial</Label>
              <Input
                type="number"
                value={trivial}
                onChange={(e) => setTrivial(e.target.value)}
                placeholder="5000"
              />
            </div>
          </div>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label className="text-xs">Basis</Label>
              <Input
                value={basis}
                onChange={(e) => setBasis(e.target.value)}
                placeholder="5% of profit before tax"
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Rationale</Label>
              <Input
                value={rationale}
                onChange={(e) => setRationale(e.target.value)}
                placeholder="Why this basis fits"
              />
            </div>
          </div>
          <Button
            size="sm"
            className="mt-4 font-semibold"
            disabled={action.isPending}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagement.id}/materiality`,
                method: "put",
                body: {
                  overallAmount: Number(overall) || 0,
                  performanceAmount: Number(performance) || 0,
                  clearlyTrivialThreshold: Number(trivial) || 0,
                  basis,
                  rationale,
                },
              })
            }
          >
            Save materiality
          </Button>
          {engagement.materiality && (
            <p className="mt-3 text-xs text-muted-foreground">
              Current: overall {fmtMoney(engagement.materiality.overallAmount)} ·
              performance {fmtMoney(engagement.materiality.performanceAmount)} ·
              trivial{" "}
              {fmtMoney(engagement.materiality.clearlyTrivialThreshold)}
            </p>
          )}
        </CardContent>
      </Card>

      <Card className="border-border/60 lg:col-span-2">
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Audit plan
          </CardTitle>
          {engagement.plan?.isApproved ? (
            <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
              <CheckCircle2 className="h-4 w-4" />
              Approved
            </span>
          ) : (
            engagement.plan && (
              <Button
                size="sm"
                variant="outline"
                className="font-semibold"
                disabled={action.isPending}
                onClick={() =>
                  action.mutate({
                    url: `/api/audit/engagements/${engagement.id}/plan/approve`,
                  })
                }
              >
                Approve plan
              </Button>
            )
          )}
        </CardHeader>
        <CardContent>
          <div className="grid gap-3 lg:grid-cols-3">
            <div className="space-y-1.5">
              <Label className="text-xs">Scope</Label>
              <Textarea
                rows={3}
                value={scope}
                onChange={(e) => setScope(e.target.value)}
                placeholder="What the audit covers"
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Objectives</Label>
              <Textarea
                rows={3}
                value={objectives}
                onChange={(e) => setObjectives(e.target.value)}
                placeholder="What it must conclude on"
              />
            </div>
            <div className="space-y-1.5">
              <Label className="text-xs">Strategy</Label>
              <Textarea
                rows={3}
                value={strategy}
                onChange={(e) => setStrategy(e.target.value)}
                placeholder="How the team will get there"
              />
            </div>
          </div>
          <Button
            size="sm"
            className="mt-4 font-semibold"
            disabled={action.isPending}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagement.id}/plan`,
                method: "put",
                body: { scope, objectives, strategy },
              })
            }
          >
            {action.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : null}
            Save plan
          </Button>
        </CardContent>
      </Card>
    </div>
  );
}

export function TeamTab({
  engagement,
  onChanged,
}: {
  engagement: EngagementDetail;
  onChanged: () => void;
}) {
  const queryClient = useQueryClient();
  const [userId, setUserId] = useState("");
  const [role, setRole] = useState<string>("Staff");

  const members = useApiQuery<OrganizationMemberRow[]>("/api/audit/users", {
    queryKey: ["organization-members"],
  });

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Team updated.");
      onChanged();
      queryClient.invalidateQueries({
        queryKey: ["audit-engagement", engagement.id],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const assignable = (members.data ?? []).filter(
    (member) =>
      !engagement.team.some((assigned) => assigned.userId === member.userId),
  );

  return (
    <div className="space-y-6">
      <Card className="border-border/60">
        <CardHeader className="pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Assign a team member
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="min-w-56 space-y-1.5">
            <Label className="text-xs">Organization member</Label>
            <FieldSelect value={userId} onChange={(e) => setUserId(e.target.value)}>
              <option value="">Select…</option>
              {assignable.map((member) => (
                <option key={member.userId} value={member.userId}>
                  {member.displayName} ({member.email})
                </option>
              ))}
            </FieldSelect>
          </div>
          <div className="space-y-1.5">
            <Label className="text-xs">Engagement role</Label>
            <FieldSelect value={role} onChange={(e) => setRole(e.target.value)}>
              {engagementRoles.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </FieldSelect>
          </div>
          <Button
            className="font-semibold"
            disabled={!userId || action.isPending}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagement.id}/team`,
                body: { userId, role },
              })
            }
          >
            <UserPlus className="mr-2 h-4 w-4" />
            Assign
          </Button>
        </CardContent>
      </Card>

      <div className="space-y-2">
        {engagement.team.map((member) => (
          <div
            key={member.memberId}
            className="flex items-center gap-3 rounded-xl border border-border/60 bg-card p-3"
          >
            <Avatar className="h-9 w-9">
              <AvatarFallback className="bg-sky-100 text-xs font-semibold text-sky-700 dark:bg-sky-950/40 dark:text-sky-400">
                {member.displayName
                  .split(" ")
                  .map((part) => part[0])
                  .join("")
                  .slice(0, 2)
                  .toUpperCase()}
              </AvatarFallback>
            </Avatar>
            <div className="flex-1">
              <div className="text-sm font-semibold">{member.displayName}</div>
              <div className="text-xs text-muted-foreground">{member.email}</div>
            </div>
            <span className="text-sm text-muted-foreground">{member.role}</span>
            <Button
              variant="ghost"
              size="icon"
              aria-label={`Remove ${member.displayName}`}
              onClick={() =>
                action.mutate({
                  url: `/api/audit/engagements/${engagement.id}/team/${member.memberId}`,
                  method: "delete",
                })
              }
            >
              <Trash2 className="h-4 w-4 text-muted-foreground" />
            </Button>
          </div>
        ))}
      </div>
    </div>
  );
}
