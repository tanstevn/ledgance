"use client";

import Link from "next/link";
import {
  ArrowRight,
  BookOpen,
  Calculator,
  CalendarClock,
  ClipboardCheck,
  ClipboardList,
  FileCheck2,
  History,
  ShieldCheck,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import { CrossSellCard } from "@/components/cross-sell";
import { StatCard, StatusPill, fmtDate } from "@/components/workspace";
import { useAuth } from "@/components/auth-context";
import { useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import { planPresentation } from "@/lib/plans";
import {
  activityActor,
  activityInitials,
  relativeTime,
} from "@/lib/activity";
import type {
  ActivityRow,
  ClientRow,
  EngagementListRow,
} from "@/lib/audit-types";
import type { EntityRow } from "@/lib/accounting-types";

const daysUntil = (dateOnly: string) =>
  Math.ceil(
    (new Date(dateOnly).getTime() - Date.now()) / (1000 * 60 * 60 * 24),
  );

function DeadlineChip({ date }: { date: string }) {
  const days = daysUntil(date);

  if (days < 0) {
    return (
      <span className="text-xs font-semibold text-red-600 dark:text-red-400">
        {Math.abs(days)}d overdue
      </span>
    );
  }
  if (days <= 30) {
    return (
      <span className="text-xs font-semibold text-amber-600 dark:text-amber-400">
        in {days}d
      </span>
    );
  }
  return (
    <span className="text-xs font-medium text-muted-foreground">
      in {days}d
    </span>
  );
}

function ActivityFeed({
  url,
  queryKey,
  enabled,
}: {
  url: string;
  queryKey: string;
  enabled: boolean;
}) {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const activity = useApiQuery<ActivityRow[]>(url, {
    queryKey: [queryKey],
    enabled,
  });

  if (activity.isLoading) {
    return (
      <div className="space-y-3">
        {[1, 2, 3].map((i) => (
          <Skeleton key={i} className="h-10 w-full rounded-lg" />
        ))}
      </div>
    );
  }

  if (activity.isError || !activity.data || activity.data.length === 0) {
    return (
      <div className="flex items-center gap-2.5 rounded-lg bg-muted/30 px-3 py-4 text-sm text-muted-foreground">
        <History className="h-4 w-4 flex-shrink-0" />
        No activity yet — it will appear here as your team works.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {activity.data.map((entry) => (
        <div key={entry.id} className="flex items-start gap-3">
          <Avatar className="h-7 w-7 flex-shrink-0">
            <AvatarFallback className="bg-muted text-[10px] font-semibold text-muted-foreground">
              {activityInitials(entry, session)}
            </AvatarFallback>
          </Avatar>
          <div className="min-w-0 flex-1">
            <p className="text-sm leading-snug">
              <span className="font-semibold">
                {activityActor(entry, session)}
              </span>{" "}
              <span className="text-muted-foreground">{entry.summary}</span>
            </p>
            <p className="mt-0.5 text-xs text-muted-foreground/70">
              {relativeTime(entry.occurredAt)}
            </p>
          </div>
        </div>
      ))}
    </div>
  );
}

function AuditOverview() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const ready = !!session && !session.needsOnboarding;

  const clients = useApiQuery<ClientRow[]>("/api/audit/clients", {
    queryKey: ["audit-clients"],
    enabled: ready,
  });
  const engagements = useApiQuery<EngagementListRow[]>(
    "/api/audit/engagements",
    { queryKey: ["audit-engagements"], enabled: ready },
  );

  const auditPlan =
    session?.plans.find((plan) => plan.module === "Audit")?.plan ?? "Free";

  if (clients.isLoading || engagements.isLoading) {
    return <Skeleton className="h-72 w-full rounded-2xl" />;
  }

  const rows = engagements.data ?? [];
  const active = rows.filter((e) => e.status !== "Completed");
  const inFieldwork = rows.filter((e) => e.status === "Fieldwork").length;
  const inReview = rows.filter(
    (e) => e.status === "Review" || e.status === "SignedOff",
  ).length;
  const dueSoon = active.filter((e) => daysUntil(e.periodEnd) <= 30).length;

  const deadlines = [...active].sort(
    (a, b) => new Date(a.periodEnd).getTime() - new Date(b.periodEnd).getTime(),
  );

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
          <ShieldCheck className="h-5 w-5 text-sky-500" />
          Audit
          <Badge variant="secondary" className="ml-1">
            {planPresentation[auditPlan]?.name ?? auditPlan} plan
          </Badge>
        </h2>
        <Link href="/dashboard/audit/engagements">
          <Button size="sm" className="font-semibold">
            View all engagements
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </Link>
      </div>

      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        <StatCard
          label={`Across ${(clients.data ?? []).filter((c) => !c.isArchived).length} active clients`}
          value={active.length}
          icon={ClipboardCheck}
          accent="text-sky-500"
        />
        <StatCard
          label="In fieldwork"
          value={inFieldwork}
          icon={ClipboardList}
          accent="text-amber-500"
        />
        <StatCard
          label="In review or signed off"
          value={inReview}
          icon={FileCheck2}
          accent="text-violet-500"
        />
        <StatCard
          label="Period ends within 30 days"
          value={dueSoon}
          icon={CalendarClock}
          accent="text-red-500"
        />
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <Card className="border-border/60 lg:col-span-2">
          <CardHeader className="pb-3">
            <CardTitle className="font-display text-base font-semibold">
              Active engagements
            </CardTitle>
          </CardHeader>
          <CardContent>
            {active.length > 0 ? (
              <div className="space-y-2.5">
                {active.slice(0, 6).map((engagement) => (
                  <Link
                    key={engagement.id}
                    href={`/dashboard/audit/engagements/${engagement.id}`}
                    className="flex items-center justify-between gap-3 rounded-xl border border-border/60 px-4 py-3 transition-colors hover:border-sky-500/40 hover:bg-muted/30"
                  >
                    <div className="min-w-0">
                      <div className="truncate text-sm font-semibold">
                        {engagement.name}
                      </div>
                      <div className="truncate text-xs text-muted-foreground">
                        {engagement.clientName} · period ends{" "}
                        {fmtDate(engagement.periodEnd)}
                      </div>
                    </div>
                    <div className="flex flex-shrink-0 items-center gap-3">
                      <DeadlineChip date={engagement.periodEnd} />
                      <StatusPill value={engagement.status} />
                    </div>
                  </Link>
                ))}
              </div>
            ) : (
              <div className="rounded-xl border border-dashed border-border p-6 text-center">
                <p className="text-sm font-medium">
                  {(clients.data ?? []).length === 0
                    ? "Start by adding your first client"
                    : "Ready for your first engagement"}
                </p>
                <Link
                  href={
                    (clients.data ?? []).length === 0
                      ? "/dashboard/audit"
                      : "/dashboard/audit/engagements"
                  }
                >
                  <Button size="sm" className="mt-3 font-semibold">
                    {(clients.data ?? []).length === 0
                      ? "Add a client"
                      : "Create engagement"}
                  </Button>
                </Link>
              </div>
            )}
          </CardContent>
        </Card>

        <div className="space-y-4">
          <Card className="border-border/60">
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 font-display text-base font-semibold">
                <CalendarClock className="h-4 w-4 text-muted-foreground" />
                Upcoming deadlines
              </CardTitle>
            </CardHeader>
            <CardContent>
              {deadlines.length > 0 ? (
                <div className="space-y-3">
                  {deadlines.slice(0, 4).map((engagement) => (
                    <Link
                      key={engagement.id}
                      href={`/dashboard/audit/engagements/${engagement.id}`}
                      className="flex items-center justify-between gap-2 rounded-lg px-1 py-0.5 transition-colors hover:bg-muted/30"
                    >
                      <div className="min-w-0">
                        <div className="truncate text-sm font-medium">
                          {engagement.name}
                        </div>
                        <div className="truncate text-xs text-muted-foreground">
                          {engagement.clientName}
                        </div>
                      </div>
                      <div className="flex-shrink-0 text-right">
                        <DeadlineChip date={engagement.periodEnd} />
                        <div className="text-xs text-muted-foreground/70">
                          {fmtDate(engagement.periodEnd)}
                        </div>
                      </div>
                    </Link>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-muted-foreground">
                  No open engagements — deadlines appear when work begins.
                </p>
              )}
            </CardContent>
          </Card>

          <Card className="border-border/60">
            <CardHeader className="pb-3">
              <CardTitle className="flex items-center gap-2 font-display text-base font-semibold">
                <History className="h-4 w-4 text-muted-foreground" />
                Recent activity
              </CardTitle>
            </CardHeader>
            <CardContent>
              <ActivityFeed
                url="/api/audit/activity?limit=6"
                queryKey="audit-recent-activity"
                enabled={ready}
              />
            </CardContent>
          </Card>
        </div>
      </div>
    </section>
  );
}

function AccountingOverview() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const ready = !!session && !session.needsOnboarding;

  const entities = useApiQuery<EntityRow[]>("/api/accounting/entities", {
    queryKey: ["accounting-entities"],
    enabled: ready,
  });

  const accountingPlan =
    session?.plans.find((plan) => plan.module === "Accounting")?.plan ?? "Free";

  if (entities.isLoading) {
    return <Skeleton className="h-64 w-full rounded-2xl" />;
  }

  const rows = (entities.data ?? []).filter((entity) => !entity.isArchived);

  return (
    <section className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 font-display text-lg font-semibold">
          <Calculator className="h-5 w-5 text-emerald-500" />
          Accounting
          <Badge variant="secondary" className="ml-1">
            {planPresentation[accountingPlan]?.name ?? accountingPlan} plan
          </Badge>
        </h2>
        <Link href="/dashboard/accounting">
          <Button size="sm" variant="outline" className="font-semibold">
            All entities
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </Link>
      </div>

      <div className="grid gap-4 lg:grid-cols-3">
        <div className="lg:col-span-2">
          {rows.length > 0 ? (
            <div className="grid gap-4 sm:grid-cols-2">
              {rows.map((entity) => (
                <Link
                  key={entity.id}
                  href={`/dashboard/accounting/${entity.id}`}
                  className="group rounded-2xl border border-border/60 bg-card p-5 transition-all hover:border-emerald-500/40 hover:shadow-md"
                >
                  <div className="flex items-center justify-between">
                    <BookOpen className="h-5 w-5 text-emerald-500" />
                    <span className="text-xs font-medium text-muted-foreground">
                      {entity.baseCurrency}
                    </span>
                  </div>
                  <div className="mt-3 font-display text-base font-semibold group-hover:text-emerald-600 dark:group-hover:text-emerald-400">
                    {entity.name}
                  </div>
                  <p className="mt-0.5 text-xs text-muted-foreground">
                    {entity.legalName || "Open the books"}
                  </p>
                  <div className="mt-3 flex items-center gap-1 text-sm font-medium text-emerald-600 dark:text-emerald-400">
                    Open books
                    <ArrowRight className="h-4 w-4 transition-transform group-hover:translate-x-0.5" />
                  </div>
                </Link>
              ))}
            </div>
          ) : (
            <Card className="border-dashed border-border">
              <CardContent className="flex flex-wrap items-center justify-between gap-4 py-6">
                <div>
                  <div className="font-semibold">
                    Open your first set of books
                  </div>
                  <p className="mt-0.5 text-sm text-muted-foreground">
                    Create an entity, build its chart of accounts, open a
                    fiscal period and post the first entry.
                  </p>
                </div>
                <Link href="/dashboard/accounting">
                  <Button className="font-semibold">
                    Create entity
                    <ArrowRight className="ml-2 h-4 w-4" />
                  </Button>
                </Link>
              </CardContent>
            </Card>
          )}
        </div>

        <Card className="border-border/60">
          <CardHeader className="pb-3">
            <CardTitle className="flex items-center gap-2 font-display text-base font-semibold">
              <History className="h-4 w-4 text-muted-foreground" />
              Recent activity
            </CardTitle>
          </CardHeader>
          <CardContent>
            <ActivityFeed
              url="/api/accounting/activity?limit=6"
              queryKey="accounting-recent-activity"
              enabled={ready}
            />
          </CardContent>
        </Card>
      </div>
    </section>
  );
}

export default function DashboardPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);

  const hasAccounting = isPlatformEnabled(session, "accounting");
  const hasAudit = isPlatformEnabled(session, "audit");

  return (
    <div className="mx-auto max-w-6xl space-y-8">
      <div>
        <h1 className="font-display text-2xl font-bold tracking-tight">
          {user?.name ? `Welcome, ${user.name.split(" ")[0]}` : "Welcome"}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          {session?.organizationName
            ? `Here is where ${session.organizationName} stands.`
            : "Here is where your organization stands."}
        </p>
      </div>

      <CrossSellCard />

      {hasAudit && <AuditOverview />}
      {hasAccounting && <AccountingOverview />}
    </div>
  );
}
