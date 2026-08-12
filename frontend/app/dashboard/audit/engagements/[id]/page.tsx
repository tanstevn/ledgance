"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import {
  AlertCircle,
  ArrowLeft,
  CalendarDays,
  Check,
  ChevronDown,
  Clock,
  FileCheck2,
  Loader2,
  MoreHorizontal,
  Pencil,
  TrendingUp,
  UserRound,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "sonner";
import { cn } from "@/lib/utils";
import { useAuth } from "@/components/auth-context";
import {
  ErrorCard,
  FieldSelect,
  LoadingRows,
  RecordAvatar,
  StatusPill,
  fmtDate,
} from "@/components/workspace";
import { useApiAction, useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import {
  engagementStatuses,
  engagementTypes,
  type EngagementDetail,
  type EvidenceRow,
  type WorkingPaperRow,
} from "@/lib/audit-types";
import {
  OverviewTab,
  PlanningTab,
  TeamTab,
  engagementGates,
} from "./planning-tabs";
import { ProceduresTab, RisksTab, TrialBalanceTab } from "./fieldwork-tabs";
import {
  ActivityTab,
  EvidenceTab,
  FindingsTab,
  ReportTab,
  WorkingPapersTab,
} from "./delivery-tabs";
import { AiTab } from "./ai-tab";

const spaced = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2");

const moreTabs: [string, string][] = [
  ["planning", "Planning"],
  ["risks", "Risks"],
  ["procedures", "Procedures"],
  ["trial-balance", "Trial balance"],
  ["findings", "Findings"],
  ["report", "Report"],
  ["ai", "AI"],
  ["activity", "Activity"],
];

const statusMeta: Record<string, { dot: string; hint: string }> = {
  Planning: { dot: "bg-amber-400", hint: "Scoping, materiality and strategy" },
  Fieldwork: { dot: "bg-sky-400", hint: "Testing and evidence gathering" },
  Review: { dot: "bg-violet-400", hint: "Manager and partner review" },
  SignedOff: { dot: "bg-emerald-400", hint: "Partner has signed off" },
  Completed: { dot: "bg-emerald-500", hint: "Closed and locked" },
};

/**
 * The status pill plus a circled chevron beside it: the dropdown lists every other stage
 * with its meaning, and the server still enforces the stage gates — an invalid move comes
 * back as the domain's own error message.
 */
function StatusMenu({
  engagement,
  onMove,
  busy,
}: {
  engagement: EngagementDetail;
  onMove: (status: string) => void;
  busy: boolean;
}) {
  return (
    <div className="flex items-center gap-2">
      <StatusPill value={engagement.status} />
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="outline"
            size="icon"
            aria-label="Change engagement status"
            disabled={busy}
            className="h-7 w-7 rounded-full"
          >
            {busy ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <ChevronDown className="h-3.5 w-3.5" />
            )}
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="start" className="w-64 rounded-xl p-1.5">
          <DropdownMenuLabel className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
            Move engagement to
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          {engagementStatuses.map((status) => {
            const meta = statusMeta[status];
            const current = status === engagement.status;

            return (
              <DropdownMenuItem
                key={status}
                disabled={current}
                onSelect={() => onMove(status)}
                className="gap-3 rounded-lg px-2.5 py-2"
              >
                <span
                  aria-hidden
                  className={cn("h-2 w-2 shrink-0 rounded-full", meta?.dot)}
                />
                <span className="flex-1">
                  <span className="block text-sm font-medium">
                    {spaced(status)}
                  </span>
                  <span className="block text-xs text-muted-foreground">
                    {meta?.hint}
                  </span>
                </span>
                {current && <Check className="h-4 w-4 text-muted-foreground" />}
              </DropdownMenuItem>
            );
          })}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}

function EditEngagementDialog({
  engagement,
  open,
  onOpenChange,
  onSaved,
}: {
  engagement: EngagementDetail;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(engagement.name);
  const [type, setType] = useState(engagement.type);
  const [periodStart, setPeriodStart] = useState(engagement.periodStart);
  const [periodEnd, setPeriodEnd] = useState(engagement.periodEnd);
  const [fiscalYearEnd, setFiscalYearEnd] = useState(
    engagement.fiscalYearEnd ?? "",
  );
  const [budgetHours, setBudgetHours] = useState(
    String(engagement.budgetHours),
  );

  const save = useApiAction({
    onSuccess: () => {
      toast.success("Engagement updated.");
      onOpenChange(false);
      onSaved();
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    save.mutate({
      url: `/api/audit/engagements/${engagement.id}`,
      method: "put",
      body: {
        name: name.trim(),
        type,
        periodStart,
        periodEnd,
        fiscalYearEnd: fiscalYearEnd || null,
        budgetHours: Number(budgetHours) || 0,
      },
    });
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>Edit engagement</DialogTitle>
            <DialogDescription>
              Details lock once the engagement is signed off.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="edit-name">Name</Label>
              <Input
                id="edit-name"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-type">Type</Label>
              <FieldSelect
                id="edit-type"
                value={type}
                onChange={(e) => setType(e.target.value)}
              >
                {engagementTypes.map((value) => (
                  <option key={value} value={value}>
                    {spaced(value)}
                  </option>
                ))}
              </FieldSelect>
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-budget">Budget hours</Label>
              <Input
                id="edit-budget"
                type="number"
                min="0"
                value={budgetHours}
                onChange={(e) => setBudgetHours(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-start">Period start</Label>
              <Input
                id="edit-start"
                type="date"
                value={periodStart}
                onChange={(e) => setPeriodStart(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-end">Period end</Label>
              <Input
                id="edit-end"
                type="date"
                value={periodEnd}
                onChange={(e) => setPeriodEnd(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="edit-fye">Fiscal year end (optional)</Label>
              <Input
                id="edit-fye"
                type="date"
                value={fiscalYearEnd}
                onChange={(e) => setFiscalYearEnd(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={save.isPending}>
              {save.isPending && (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              )}
              Save changes
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function HeaderStat({
  icon: Icon,
  value,
  label,
  tone = "text-primary",
  alert,
}: {
  icon: typeof TrendingUp;
  value: string | number;
  label: string;
  tone?: string;
  alert?: boolean;
}) {
  return (
    <div className="flex items-center gap-3 rounded-2xl border border-border/60 bg-card p-4">
      <span
        className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-muted ${tone}`}
      >
        <Icon className="h-4 w-4" />
      </span>
      <div className="min-w-0">
        <div className="font-display text-xl font-bold leading-none">
          {value}
        </div>
        <div className="mt-1 truncate text-xs text-muted-foreground">
          {label}
          {alert && (
            <span className="ml-1 font-medium text-destructive">
              · needs attention
            </span>
          )}
        </div>
      </div>
    </div>
  );
}

export default function EngagementWorkspace({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const [tab, setTab] = useState<string>("overview");
  const [editing, setEditing] = useState(false);
  const ready =
    !!session &&
    !session.needsOnboarding &&
    isPlatformEnabled(session, "audit");

  const detail = useApiQuery<EngagementDetail>(`/api/audit/engagements/${id}`, {
    queryKey: ["audit-engagement", id],
    enabled: ready,
  });

  const papers = useApiQuery<WorkingPaperRow[]>(
    `/api/audit/engagements/${id}/working-papers`,
    { queryKey: ["audit-papers", id], enabled: ready },
  );

  const evidence = useApiQuery<EvidenceRow[]>(
    `/api/audit/engagements/${id}/evidence`,
    { queryKey: ["audit-evidence", id], enabled: ready },
  );

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["audit-engagement", id] });

  const changeStatus = useApiAction<string>({
    onSuccess: () => {
      toast.success("Engagement status updated.");
      refresh();
      queryClient.invalidateQueries({
        queryKey: ["/api/audit/engagements/paged"],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  if (!ready || detail.isLoading) {
    return (
      <div className="space-y-4">
        <LoadingRows count={5} />
      </div>
    );
  }

  if (detail.isError || !detail.data) {
    return (
      <div className="mx-auto max-w-3xl">
        <ErrorCard
          title="Could not load this engagement"
          errors={detail.error}
          onRetry={() => detail.refetch()}
        />
      </div>
    );
  }

  const engagement = detail.data;
  const progress = engagement.progress;
  const paperRows = papers.data ?? [];
  const evidenceRows = evidence.data ?? [];
  const approvedPapers = paperRows.filter(
    (paper) => paper.status === "Approved",
  ).length;

  const gates = engagementGates(engagement);
  const cleared = gates.filter((gate) => gate.cleared).length;
  const progressPercent = Math.round((cleared / gates.length) * 100);

  const partner = engagement.team.find((member) => member.role === "Partner");
  const manager = engagement.team.find((member) => member.role === "Manager");

  const activeMore = moreTabs.find(([value]) => value === tab);

  return (
    <div className="space-y-5">
      <Link
        href="/dashboard/audit/engagements"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="h-4 w-4" />
        Back to engagements
      </Link>

      <header className="rounded-2xl border border-border/60 bg-card p-5">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div className="flex min-w-0 items-start gap-3">
            <RecordAvatar
              name={engagement.clientName || engagement.name}
              className="h-11 w-11"
            />
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-3">
                <h1 className="font-display text-xl font-bold tracking-tight">
                  {engagement.name}
                </h1>
                <StatusMenu
                  engagement={engagement}
                  busy={changeStatus.isPending}
                  onMove={(targetStatus) =>
                    changeStatus.mutate({
                      url: `/api/audit/engagements/${id}/status`,
                      body: { targetStatus },
                    })
                  }
                />
              </div>
              <p className="mt-0.5 text-sm text-muted-foreground">
                {engagement.clientName}
              </p>
              <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
                <span className="flex items-center gap-1">
                  <UserRound className="h-3.5 w-3.5" />
                  Partner:{" "}
                  <span className="font-medium text-foreground">
                    {partner?.displayName || "Unassigned"}
                  </span>
                </span>
                <span className="flex items-center gap-1">
                  <UserRound className="h-3.5 w-3.5" />
                  Manager:{" "}
                  <span className="font-medium text-foreground">
                    {manager?.displayName || "Unassigned"}
                  </span>
                </span>
                <span className="flex items-center gap-1">
                  <CalendarDays className="h-3.5 w-3.5" />
                  {fmtDate(engagement.periodStart)} —{" "}
                  {fmtDate(engagement.periodEnd)}
                </span>
                <span className="flex items-center gap-1">
                  <TrendingUp className="h-3.5 w-3.5" />
                  FYE: {fmtDate(engagement.fiscalYearEnd)}
                </span>
              </div>
            </div>
          </div>

          <Button
            variant="outline"
            className="font-semibold"
            onClick={() => setEditing(true)}
          >
            <Pencil className="mr-2 h-4 w-4" />
            Edit
          </Button>
        </div>
      </header>

      <EditEngagementDialog
        key={`${engagement.id}-${engagement.name}-${engagement.periodStart}`}
        engagement={engagement}
        open={editing}
        onOpenChange={setEditing}
        onSaved={refresh}
      />

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <HeaderStat
          icon={TrendingUp}
          value={`${progressPercent}%`}
          label={`Progress · ${cleared} of ${gates.length} stage gates clear`}
        />
        <HeaderStat
          icon={Clock}
          value={`${engagement.budgetHours}h`}
          label={`Budget · ${engagement.team.length} team ${
            engagement.team.length === 1 ? "member" : "members"
          }`}
        />
        <HeaderStat
          icon={FileCheck2}
          value={paperRows.length}
          label={`Working papers · ${approvedPapers} approved`}
        />
        <HeaderStat
          icon={AlertCircle}
          value={progress.openReviewNotes}
          label="Open review notes"
          tone={
            progress.openReviewNotes > 0 ? "text-destructive" : "text-primary"
          }
          alert={progress.openReviewNotes > 0}
        />
      </div>

      <Tabs value={tab} onValueChange={setTab}>
        <div className="flex items-center gap-1 rounded-xl border border-border/60 bg-card p-1">
          <TabsList className="h-auto flex-1 justify-stretch gap-1 bg-transparent p-0">
            {(
              [
                ["overview", "Overview", null],
                ["documents", "Documents", evidenceRows.length],
                ["papers", "Working Papers", paperRows.length],
                ["team", "Team", null],
              ] as [string, string, number | null][]
            ).map(([value, label, count]) => (
              <TabsTrigger
                key={value}
                value={value}
                className="flex-1 rounded-lg px-3 py-2 text-sm data-[state=active]:bg-muted data-[state=active]:font-semibold"
              >
                {label}
                {count !== null && count > 0 && (
                  <span className="ml-1.5 text-xs text-muted-foreground">
                    {count}
                  </span>
                )}
              </TabsTrigger>
            ))}
          </TabsList>

          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button
                variant="ghost"
                size="sm"
                className={cn(
                  "rounded-lg px-3 py-2 text-sm text-muted-foreground",
                  activeMore && "bg-muted font-semibold text-foreground",
                )}
              >
                <MoreHorizontal className="h-4 w-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48 rounded-xl p-1.5">
              {moreTabs.map(([value, label]) => (
                <DropdownMenuItem
                  key={value}
                  onSelect={() => setTab(value)}
                  className="rounded-lg px-2.5 py-2 text-sm"
                >
                  <span className="flex-1">{label}</span>
                  {tab === value && (
                    <Check className="h-4 w-4 text-muted-foreground" />
                  )}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        <TabsContent value="overview" className="mt-6">
          <OverviewTab
            engagement={engagement}
            papers={paperRows}
            onNavigate={setTab}
          />
        </TabsContent>
        <TabsContent value="documents" className="mt-6">
          <EvidenceTab engagementId={id} />
        </TabsContent>
        <TabsContent value="papers" className="mt-6">
          <WorkingPapersTab engagementId={id} />
        </TabsContent>
        <TabsContent value="team" className="mt-6">
          <TeamTab engagement={engagement} onChanged={refresh} />
        </TabsContent>
        <TabsContent value="planning" className="mt-6">
          <PlanningTab engagement={engagement} onChanged={refresh} />
        </TabsContent>
        <TabsContent value="risks" className="mt-6">
          <RisksTab engagementId={id} />
        </TabsContent>
        <TabsContent value="procedures" className="mt-6">
          <ProceduresTab engagementId={id} />
        </TabsContent>
        <TabsContent value="trial-balance" className="mt-6">
          <TrialBalanceTab engagementId={id} />
        </TabsContent>
        <TabsContent value="findings" className="mt-6">
          <FindingsTab engagementId={id} />
        </TabsContent>
        <TabsContent value="report" className="mt-6">
          <ReportTab engagementId={id} />
        </TabsContent>
        <TabsContent value="ai" className="mt-6">
          <AiTab engagementId={id} />
        </TabsContent>
        <TabsContent value="activity" className="mt-6">
          <ActivityTab engagementId={id} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
