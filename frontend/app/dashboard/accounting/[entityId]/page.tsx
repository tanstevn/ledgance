"use client";

import { use } from "react";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useAuth } from "@/components/auth-context";
import { ErrorCard, LoadingRows } from "@/components/workspace";
import { useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import type { EntityRow } from "@/lib/accounting-types";
import { AccountsTab, JournalTab, PeriodsTab } from "./books-tabs";
import {
  ActivityTab,
  DocumentsTab,
  ReconciliationsTab,
  ReportsTab,
} from "./insight-tabs";
import { AiTab } from "./ai-tab";

export default function EntityWorkspace({
  params,
}: {
  params: Promise<{ entityId: string }>;
}) {
  const { entityId } = use(params);
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const ready =
    !!session &&
    !session.needsOnboarding &&
    isPlatformEnabled(session, "accounting");

  const entity = useApiQuery<EntityRow>(`/api/accounting/entities/${entityId}`, {
    queryKey: ["accounting-entity", entityId],
    enabled: ready,
  });

  if (!ready || entity.isLoading) {
    return (
      <div className="mx-auto max-w-6xl">
        <LoadingRows count={5} />
      </div>
    );
  }

  if (entity.isError || !entity.data) {
    return (
      <div className="mx-auto max-w-3xl">
        <ErrorCard
          title="Could not load this entity"
          errors={entity.error}
          onRetry={() => entity.refetch()}
        />
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <Link
          href="/dashboard/accounting"
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Entities
        </Link>
        <div className="mt-2 flex flex-wrap items-center justify-between gap-4">
          <div>
            <h1 className="font-display text-2xl font-bold tracking-tight">
              {entity.data.name}
            </h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {entity.data.legalName || "The books"} · base currency{" "}
              {entity.data.baseCurrency}
            </p>
          </div>
          {entity.data.isArchived && <Badge variant="secondary">Archived</Badge>}
        </div>
      </div>

      <Tabs defaultValue="accounts">
        <div className="overflow-x-auto">
          <TabsList className="h-auto flex-wrap justify-start gap-1 bg-transparent p-0">
            {[
              ["accounts", "Chart of accounts"],
              ["periods", "Fiscal periods"],
              ["journal", "Journal"],
              ["reports", "Ledger & reports"],
              ["reconciliations", "Reconciliation"],
              ["documents", "Documents"],
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

        <TabsContent value="accounts" className="mt-6">
          <AccountsTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="periods" className="mt-6">
          <PeriodsTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="journal" className="mt-6">
          <JournalTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="reports" className="mt-6">
          <ReportsTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="reconciliations" className="mt-6">
          <ReconciliationsTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="documents" className="mt-6">
          <DocumentsTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="ai" className="mt-6">
          <AiTab entityId={entityId} />
        </TabsContent>
        <TabsContent value="activity" className="mt-6">
          <ActivityTab entityId={entityId} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
