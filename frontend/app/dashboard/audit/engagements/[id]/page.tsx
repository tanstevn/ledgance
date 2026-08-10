"use client";

import { use } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { ArrowLeft } from "lucide-react";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import {
  ErrorCard,
  FieldSelect,
  LoadingRows,
  StatusPill,
} from "@/components/workspace";
import { useApiAction, useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import { engagementStatuses, type EngagementDetail } from "@/lib/audit-types";
import { OverviewTab, TeamTab } from "./planning-tabs";
import { ProceduresTab, RisksTab, TrialBalanceTab } from "./fieldwork-tabs";
import {
  ActivityTab,
  EvidenceTab,
  FindingsTab,
  ReportTab,
  WorkingPapersTab,
} from "./delivery-tabs";
import { AiTab } from "./ai-tab";

export default function EngagementWorkspace({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = use(params);
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const ready =
    !!session && !session.needsOnboarding && isPlatformEnabled(session, "audit");

  const detail = useApiQuery<EngagementDetail>(`/api/audit/engagements/${id}`, {
    queryKey: ["audit-engagement", id],
    enabled: ready,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["audit-engagement", id] });

  const changeStatus = useApiAction<string>({
    onSuccess: () => {
      toast.success("Engagement status updated.");
      refresh();
      queryClient.invalidateQueries({ queryKey: ["audit-engagements"] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  if (!ready || detail.isLoading) {
    return (
      <div className="mx-auto max-w-6xl">
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

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <Link
          href="/dashboard/audit/engagements"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Engagements
        </Link>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="font-display text-2xl font-bold tracking-tight">
              {engagement.name}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {engagement.clientName} ·{" "}
              {engagement.type.replace(/([a-z])([A-Z])/g, "$1 $2")}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <StatusPill value={engagement.status} />
            <FieldSelect
              className="w-44"
              value=""
              onChange={(e) => {
                if (e.target.value) {
                  changeStatus.mutate({
                    url: `/api/audit/engagements/${id}/status`,
                    body: { targetStatus: e.target.value },
                  });
                }
              }}
              disabled={changeStatus.isPending}
            >
              <option value="">Move to…</option>
              {engagementStatuses
                .filter((status) => status !== engagement.status)
                .map((status) => (
                  <option key={status} value={status}>
                    {status.replace(/([a-z])([A-Z])/g, "$1 $2")}
                  </option>
                ))}
            </FieldSelect>
          </div>
        </div>
      </div>

      <Tabs defaultValue="overview">
        <div className="overflow-x-auto">
          <TabsList className="h-auto flex-wrap justify-start gap-1 bg-transparent p-0">
            {[
              ["overview", "Overview"],
              ["team", "Team"],
              ["risks", "Risks"],
              ["procedures", "Procedures"],
              ["papers", "Working papers"],
              ["evidence", "Evidence"],
              ["trial-balance", "Trial balance"],
              ["findings", "Findings"],
              ["report", "Report"],
              ["ai", "AI"],
              ["activity", "Activity"],
            ].map(([value, label]) => (
              <TabsTrigger
                key={value}
                value={value}
                className="rounded-lg border border-transparent px-3 py-1.5 text-sm data-[state=active]:border-border/60 data-[state=active]:bg-card data-[state=active]:shadow-sm"
              >
                {label}
              </TabsTrigger>
            ))}
          </TabsList>
        </div>

        <TabsContent value="overview" className="mt-6">
          <OverviewTab engagement={engagement} onChanged={refresh} />
        </TabsContent>
        <TabsContent value="team" className="mt-6">
          <TeamTab engagement={engagement} onChanged={refresh} />
        </TabsContent>
        <TabsContent value="risks" className="mt-6">
          <RisksTab engagementId={id} />
        </TabsContent>
        <TabsContent value="procedures" className="mt-6">
          <ProceduresTab engagementId={id} />
        </TabsContent>
        <TabsContent value="papers" className="mt-6">
          <WorkingPapersTab engagementId={id} />
        </TabsContent>
        <TabsContent value="evidence" className="mt-6">
          <EvidenceTab engagementId={id} />
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
