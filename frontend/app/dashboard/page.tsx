"use client";

import Link from "next/link";
import {
  Building2,
  ClipboardCheck,
  FileText,
  Clock,
  TrendingUp,
  AlertTriangle,
  CheckCircle2,
  ArrowRight,
  FileCheck2,
  MessageSquare,
  GitBranch,
  FileSpreadsheet,
  Calendar,
} from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  engagements,
  clients,
  workingPapers,
  documents,
  activityFeed,
  statusConfig,
  signOffConfig,
} from "@/lib/mock-data";

const activityIcons: Record<string, typeof FileCheck2> = {
  "sign-off": FileCheck2,
  document: GitBranch,
  note: MessageSquare,
  engagement: ClipboardCheck,
  mapping: FileSpreadsheet,
};

function formatRelativeTime(timestamp: string) {
  const date = new Date(timestamp);
  const now = new Date("2025-05-20T18:00:00");
  const diffMs = now.getTime() - date.getTime();
  const diffHours = Math.floor(diffMs / (1000 * 60 * 60));
  if (diffHours < 1) return "just now";
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  return `${diffDays}d ago`;
}

export default function DashboardPage() {
  const activeEngagements = engagements.filter(
    (e) => e.status !== "completed",
  ).length;
  const papersForReview = workingPapers.filter(
    (wp) => wp.signOffStatus === "prepared" || wp.signOffStatus === "pending",
  ).length;
  const totalBudget = engagements.reduce((sum, e) => sum + e.budgetHours, 0);
  const totalActual = engagements.reduce((sum, e) => sum + e.actualHours, 0);
  const budgetUtilization = Math.round((totalActual / totalBudget) * 100);
  const totalDocuments = documents.length;

  const statusCounts = engagements.reduce(
    (acc, e) => {
      acc[e.status] = (acc[e.status] || 0) + 1;
      return acc;
    },
    {} as Record<string, number>,
  );

  const upcomingDeadlines = engagements
    .filter((e) => e.status !== "completed")
    .sort(
      (a, b) => new Date(a.endDate).getTime() - new Date(b.endDate).getTime(),
    )
    .slice(0, 4);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-center">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight">
            Dashboard
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Overview of your audit engagements and team activity.
          </p>
        </div>
        <Link href="/dashboard/engagements">
          <Button>
            View all engagements
            <ArrowRight className="ml-2 h-4 w-4" />
          </Button>
        </Link>
      </div>

      {/* Stats grid */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {[
          {
            label: "Active Engagements",
            value: activeEngagements,
            icon: ClipboardCheck,
            color: "text-sky-500",
            bg: "bg-sky-50 dark:bg-sky-950/40",
            sub: `${clients.length} clients`,
          },
          {
            label: "Papers for Review",
            value: papersForReview,
            icon: FileText,
            color: "text-amber-500",
            bg: "bg-amber-50 dark:bg-amber-950/40",
            sub: "Awaiting sign-off",
          },
          {
            label: "Budget Utilization",
            value: `${budgetUtilization}%`,
            icon: TrendingUp,
            color: "text-emerald-500",
            bg: "bg-emerald-50 dark:bg-emerald-950/40",
            sub: `${totalActual}h / ${totalBudget}h`,
          },
          {
            label: "Documents",
            value: totalDocuments,
            icon: FileText,
            color: "text-violet-500",
            bg: "bg-violet-50 dark:bg-violet-950/40",
            sub: "Versioned & tracked",
          },
        ].map((stat) => (
          <Card key={stat.label} className="overflow-hidden">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium text-muted-foreground">
                {stat.label}
              </CardTitle>
              <div
                className={`flex h-9 w-9 items-center justify-center rounded-lg ${stat.bg}`}
              >
                <stat.icon className={`h-4.5 w-4.5 ${stat.color}`} />
              </div>
            </CardHeader>
            <CardContent>
              <div className="font-display text-2xl font-bold">
                {stat.value}
              </div>
              <p className="mt-1 text-xs text-muted-foreground">{stat.sub}</p>
            </CardContent>
          </Card>
        ))}
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        {/* Left column - engagements & deadlines */}
        <div className="space-y-6 lg:col-span-2">
          {/* Engagement status overview */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="text-base font-semibold">
                  Engagement Status Overview
                </CardTitle>
                <Link
                  href="/dashboard/engagements"
                  className="text-xs font-medium text-primary hover:underline"
                >
                  View all
                </Link>
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-4">
                {Object.entries(statusCounts).map(([status, count]) => {
                  const config = statusConfig[status];
                  const percentage = Math.round(
                    (count / engagements.length) * 100,
                  );
                  return (
                    <div key={status} className="flex items-center gap-4">
                      <div className="flex w-28 items-center gap-2">
                        <span
                          className={`h-2 w-2 rounded-full ${config.dot}`}
                        />
                        <span className="text-sm font-medium">
                          {config.label}
                        </span>
                      </div>
                      <div className="h-2 flex-1 overflow-hidden rounded-full bg-muted">
                        <div
                          className={`h-full rounded-full ${config.dot}`}
                          style={{ width: `${percentage}%` }}
                        />
                      </div>
                      <span className="w-8 text-right text-sm font-semibold">
                        {count}
                      </span>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>

          {/* Active engagements list */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="text-base font-semibold">
                  Active Engagements
                </CardTitle>
                <Link
                  href="/dashboard/engagements"
                  className="text-xs font-medium text-primary hover:underline"
                >
                  View all
                </Link>
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {engagements
                  .filter((e) => e.status !== "completed")
                  .slice(0, 4)
                  .map((eng) => {
                    const client = clients.find((c) => c.id === eng.clientId);
                    const config = statusConfig[eng.status];
                    return (
                      <Link
                        key={eng.id}
                        href={`/dashboard/engagements/${eng.id}`}
                        className="flex items-center gap-4 rounded-lg border border-border/60 p-3 transition-colors hover:bg-muted/30"
                      >
                        <div
                          className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg text-sm font-bold text-white"
                          style={{
                            backgroundColor: client?.logoColor || "#0ea5e9",
                          }}
                        >
                          {client?.name.charAt(0)}
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="truncate text-sm font-semibold">
                            {eng.name}
                          </div>
                          <div className="truncate text-xs text-muted-foreground">
                            {client?.name}
                          </div>
                        </div>
                        <div className="hidden sm:block">
                          <div className="flex items-center gap-2">
                            <div className="h-1.5 w-20 overflow-hidden rounded-full bg-muted">
                              <div
                                className="h-full rounded-full bg-primary"
                                style={{ width: `${eng.progress}%` }}
                              />
                            </div>
                            <span className="text-xs font-medium text-muted-foreground">
                              {eng.progress}%
                            </span>
                          </div>
                        </div>
                        <Badge className={config.color} variant="secondary">
                          {config.label}
                        </Badge>
                      </Link>
                    );
                  })}
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Right column - activity & deadlines */}
        <div className="space-y-6">
          {/* Upcoming deadlines */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle className="text-base font-semibold">
                  Upcoming Deadlines
                </CardTitle>
                <Calendar className="h-4 w-4 text-muted-foreground" />
              </div>
            </CardHeader>
            <CardContent>
              <div className="space-y-3">
                {upcomingDeadlines.map((eng) => {
                  const client = clients.find((c) => c.id === eng.clientId);
                  const daysLeft = Math.ceil(
                    (new Date(eng.endDate).getTime() -
                      new Date("2025-05-20").getTime()) /
                      (1000 * 60 * 60 * 24),
                  );
                  const urgent = daysLeft <= 30;
                  return (
                    <Link
                      key={eng.id}
                      href={`/dashboard/engagements/${eng.id}`}
                      className="flex items-center gap-3 rounded-lg p-2 transition-colors hover:bg-muted/30"
                    >
                      <div
                        className={`flex h-9 w-9 flex-shrink-0 items-center justify-center rounded-lg ${
                          urgent
                            ? "bg-red-50 text-red-600 dark:bg-red-950 dark:text-red-400"
                            : "bg-amber-50 text-amber-600 dark:bg-amber-950 dark:text-amber-400"
                        }`}
                      >
                        <Clock className="h-4 w-4" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="truncate text-sm font-medium">
                          {eng.name}
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {client?.name}
                        </div>
                      </div>
                      <div className="text-right">
                        <div
                          className={`text-xs font-semibold ${
                            urgent
                              ? "text-red-600 dark:text-red-400"
                              : "text-foreground"
                          }`}
                        >
                          {daysLeft} days
                        </div>
                        <div className="text-xs text-muted-foreground">
                          {new Date(eng.endDate).toLocaleDateString("en-US", {
                            month: "short",
                            day: "numeric",
                          })}
                        </div>
                      </div>
                    </Link>
                  );
                })}
              </div>
            </CardContent>
          </Card>

          {/* Recent activity */}
          <Card>
            <CardHeader>
              <CardTitle className="text-base font-semibold">
                Recent Activity
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="space-y-1">
                {activityFeed.slice(0, 6).map((activity) => {
                  const Icon = activityIcons[activity.type] || FileCheck2;
                  return (
                    <div
                      key={activity.id}
                      className="flex items-start gap-3 rounded-lg p-2 transition-colors hover:bg-muted/30"
                    >
                      <Avatar className="h-7 w-7 flex-shrink-0">
                        <AvatarFallback className="bg-gradient-to-br from-sky-400 to-emerald-400 text-[10px] font-semibold text-white">
                          {activity.actorInitials}
                        </AvatarFallback>
                      </Avatar>
                      <div className="min-w-0 flex-1">
                        <p className="text-xs leading-relaxed">
                          <span className="font-semibold">
                            {activity.actor}
                          </span>{" "}
                          <span className="text-muted-foreground">
                            {activity.action}
                          </span>{" "}
                          <span className="font-medium">{activity.target}</span>
                        </p>
                        <p className="mt-0.5 text-xs text-muted-foreground">
                          {formatRelativeTime(activity.timestamp)}
                        </p>
                      </div>
                    </div>
                  );
                })}
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
