"use client";

import { AlertCircle, ChevronDown, RefreshCw, type LucideIcon } from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

export function LoadingRows({ count = 3 }: { count?: number }) {
  return (
    <div className="space-y-3">
      {Array.from({ length: count }).map((_, i) => (
        <Skeleton key={i} className="h-12 w-full rounded-xl" />
      ))}
    </div>
  );
}

export function ErrorCard({
  title = "Something went wrong",
  errors,
  onRetry,
}: {
  title?: string;
  errors?: string[] | null;
  onRetry?: () => void;
}) {
  return (
    <div className="rounded-2xl border border-border/60 bg-card p-8 text-center">
      <AlertCircle className="mx-auto h-7 w-7 text-destructive" />
      <h3 className="mt-3 font-display text-base font-semibold">{title}</h3>
      {errors && errors.length > 0 && (
        <p className="mt-1.5 text-sm text-muted-foreground">
          {errors.join(" ")}
        </p>
      )}
      {onRetry && (
        <Button variant="outline" size="sm" className="mt-4" onClick={onRetry}>
          <RefreshCw className="mr-2 h-4 w-4" />
          Retry
        </Button>
      )}
    </div>
  );
}

export function EmptyCard({
  icon: Icon,
  title,
  body,
  action,
}: {
  icon: LucideIcon;
  title: string;
  body: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="rounded-2xl border border-dashed border-border bg-card p-10 text-center">
      <Icon className="mx-auto h-7 w-7 text-muted-foreground" />
      <h3 className="mt-3 font-display text-base font-semibold">{title}</h3>
      <p className="mx-auto mt-1.5 max-w-sm text-sm text-muted-foreground">
        {body}
      </p>
      {action && <div className="mt-5 flex justify-center">{action}</div>}
    </div>
  );
}

const statusTones: Record<string, string> = {
  // shared
  Open: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  Closed: "bg-muted text-muted-foreground",
  Completed: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  // engagements
  Planning: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400",
  Fieldwork: "bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400",
  Review: "bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-400",
  SignedOff: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  // working papers / procedures
  Draft: "bg-muted text-muted-foreground",
  Prepared: "bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400",
  Reviewed: "bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-400",
  Approved: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  Planned: "bg-muted text-muted-foreground",
  InProgress: "bg-sky-100 text-sky-700 dark:bg-sky-950/40 dark:text-sky-400",
  NotApplicable: "bg-muted text-muted-foreground",
  // journal
  Posted: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  Reversed: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400",
  // findings / risks
  Low: "bg-muted text-muted-foreground",
  Medium: "bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-400",
  High: "bg-orange-100 text-orange-700 dark:bg-orange-950/40 dark:text-orange-400",
  Critical: "bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-400",
  Resolved: "bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-400",
  RiskAccepted: "bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-400",
  // reconciliation
  Cancelled: "bg-muted text-muted-foreground",
};

export function StatusPill({ value }: { value: string }) {
  return (
    <Badge
      className={cn(
        "pointer-events-none font-medium",
        statusTones[value] ?? "bg-muted text-muted-foreground",
      )}
    >
      {value.replace(/([a-z])([A-Z])/g, "$1 $2")}
    </Badge>
  );
}

export function StatCard({
  label,
  value,
  icon: Icon,
  accent = "text-primary",
}: {
  label: string;
  value: string | number;
  icon: LucideIcon;
  accent?: string;
}) {
  return (
    <div className="rounded-2xl border border-border/60 bg-card p-5">
      <Icon className={cn("h-5 w-5", accent)} />
      <div className="mt-3 font-display text-2xl font-bold">{value}</div>
      <div className="mt-0.5 text-xs text-muted-foreground">{label}</div>
    </div>
  );
}

/** Native select styled to match the Input component — lighter than the Radix select. */
export function FieldSelect(props: React.SelectHTMLAttributes<HTMLSelectElement>) {
  const { className, ...rest } = props;
  return (
    <span className={cn("relative block", className)}>
      <select
        className="flex h-10 w-full appearance-none rounded-md border border-input bg-background py-2 pl-3 pr-9 text-sm ring-offset-background focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
        {...rest}
      />
      <ChevronDown className="pointer-events-none absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
    </span>
  );
}

export const fmtDate = (value: string | undefined | null) =>
  value ? new Date(value).toLocaleDateString() : "—";

export const fmtMoney = (value: number) =>
  value.toLocaleString(undefined, {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  });

export const fmtBytes = (bytes: number) => {
  if (bytes >= 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${bytes} B`;
};
