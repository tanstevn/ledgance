"use client";

import { useEffect, useRef, useState } from "react";
import {
  AlertCircle,
  Calendar as CalendarIcon,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  FileText,
  RefreshCw,
  UploadCloud,
  X,
  type LucideIcon,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Calendar } from "@/components/ui/calendar";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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

const avatarTones = [
  "bg-chart-1 text-white",
  "bg-chart-2 text-white",
  "bg-chart-3 text-white",
  "bg-chart-5 text-white",
  "bg-chart-4 text-white",
];

/**
 * Identity tile for a record card. The tone is derived from the name so the same client or
 * entity keeps its colour across pages without storing one.
 */
export function RecordAvatar({
  name,
  className,
}: {
  name: string;
  className?: string;
}) {
  const seed = [...name].reduce((total, char) => total + char.charCodeAt(0), 0);

  return (
    <span
      aria-hidden
      className={cn(
        "flex h-10 w-10 shrink-0 items-center justify-center rounded-xl font-display text-base font-bold",
        avatarTones[seed % avatarTones.length],
        className,
      )}
    >
      {name.trim().charAt(0).toUpperCase() || "?"}
    </span>
  );
}

/** Initials bubble for a person, used in the stacked team avatars on list rows. */
export function MemberAvatar({ name }: { name: string }) {
  const initials = name
    .split(" ")
    .filter(Boolean)
    .map((part) => part[0])
    .join("")
    .slice(0, 2)
    .toUpperCase();

  return (
    <span
      title={name}
      className="flex h-7 w-7 items-center justify-center rounded-full border-2 border-card bg-muted text-[10px] font-semibold text-foreground"
    >
      {initials || "?"}
    </span>
  );
}

export function ProgressTrack({
  value,
  max,
  className,
  tone = "bg-primary",
}: {
  value: number;
  max: number;
  className?: string;
  tone?: string;
}) {
  const percent = max > 0 ? Math.min(100, Math.round((value / max) * 100)) : 0;

  return (
    <div
      className={cn("h-1.5 w-full overflow-hidden rounded-full bg-muted", className)}
      role="progressbar"
      aria-valuenow={percent}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div className={cn("h-full rounded-full", tone)} style={{ width: `${percent}%` }} />
    </div>
  );
}

/** Page numbers with ellipsis — always shows the first, last and the window around current. */
function pageWindow(current: number, total: number): (number | "gap")[] {
  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  const pages = new Set<number>([1, total, current]);

  for (const offset of [-1, 1]) {
    const page = current + offset;
    if (page > 1 && page < total) pages.add(page);
  }

  if (current <= 3) [2, 3, 4].forEach((page) => pages.add(page));
  if (current >= total - 2)
    [total - 3, total - 2, total - 1].forEach((page) => pages.add(page));

  const ordered = [...pages].filter((page) => page >= 1 && page <= total).sort((a, b) => a - b);

  return ordered.flatMap((page, index) =>
    index > 0 && page - ordered[index - 1] > 1 ? ["gap" as const, page] : [page],
  );
}

export function Pagination({
  page,
  totalPages,
  totalResults,
  onChange,
}: {
  page: number;
  totalPages: number;
  totalResults?: number;
  onChange: (page: number) => void;
}) {
  if (totalPages <= 1) {
    return totalResults ? (
      <p className="text-xs text-muted-foreground">
        {totalResults} {totalResults === 1 ? "record" : "records"}
      </p>
    ) : null;
  }

  return (
    <nav
      aria-label="Pagination"
      className="flex flex-wrap items-center justify-between gap-3"
    >
      <p className="text-xs text-muted-foreground">
        Page {page} of {totalPages}
        {totalResults !== undefined && ` · ${totalResults} records`}
      </p>
      <div className="flex items-center gap-1">
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          aria-label="Previous page"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
        >
          <ChevronLeft className="h-4 w-4" />
        </Button>
        {pageWindow(page, totalPages).map((entry, index) =>
          entry === "gap" ? (
            <span
              key={`gap-${index}`}
              className="px-1.5 text-sm text-muted-foreground"
              aria-hidden
            >
              …
            </span>
          ) : (
            <Button
              key={entry}
              variant={entry === page ? "default" : "outline"}
              size="icon"
              className="h-8 w-8 text-sm font-semibold"
              aria-label={`Page ${entry}`}
              aria-current={entry === page ? "page" : undefined}
              onClick={() => onChange(entry)}
            >
              {entry}
            </Button>
          ),
        )}
        <Button
          variant="outline"
          size="icon"
          className="h-8 w-8"
          aria-label="Next page"
          disabled={page >= totalPages}
          onClick={() => onChange(page + 1)}
        >
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>
    </nav>
  );
}

/**
 * Fires once the sentinel scrolls into view — the trigger that asks the API for the next page
 * of cards. Rendered only while another page exists.
 */
export function InfiniteScrollSentinel({
  onReach,
  disabled,
}: {
  onReach: () => void;
  disabled?: boolean;
}) {
  const ref = useRef<HTMLDivElement | null>(null);
  const callback = useRef(onReach);

  useEffect(() => {
    callback.current = onReach;
  }, [onReach]);

  useEffect(() => {
    const element = ref.current;

    if (!element || disabled) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          callback.current();
        }
      },
      { rootMargin: "240px" },
    );

    observer.observe(element);
    return () => observer.disconnect();
  }, [disabled]);

  return <div ref={ref} aria-hidden className="h-px w-full" />;
}

/**
 * File picker: a drop target that is also clickable and keyboard-operable. Selected files
 * list as chips beneath the zone, each individually removable, so what will be sent is
 * unambiguous. Dropping or browsing again adds to the selection (duplicates by name+size
 * are ignored).
 */
export function FileDropZone({
  files,
  onSelect,
  disabled,
  single,
}: {
  files: File[];
  onSelect: (files: File[]) => void;
  disabled?: boolean;
  /** Accept exactly one file: a new drop or browse replaces the current selection. */
  single?: boolean;
}) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const [dragging, setDragging] = useState(false);

  const browse = () => inputRef.current?.click();

  const add = (incoming: FileList | null) => {
    if (!incoming || incoming.length === 0) return;

    if (single) {
      onSelect([incoming[0]]);
      return;
    }

    const merged = [...files];

    for (const file of incoming) {
      if (!merged.some((f) => f.name === file.name && f.size === file.size)) {
        merged.push(file);
      }
    }

    onSelect(merged);
  };

  return (
    <div className="space-y-2.5">
      <div
        role="button"
        tabIndex={disabled ? -1 : 0}
        aria-label={
          single
            ? "Drag and drop a file here, or browse"
            : "Drag and drop files here, or browse"
        }
        onClick={browse}
        onKeyDown={(e) => {
          if (e.key === "Enter" || e.key === " ") {
            e.preventDefault();
            browse();
          }
        }}
        onDragOver={(e) => {
          e.preventDefault();
          if (!disabled) setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          if (!disabled) add(e.dataTransfer.files);
        }}
        className={cn(
          "flex cursor-pointer flex-col items-center justify-center gap-3 rounded-2xl border-2 border-dashed px-6 text-center transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring",
          files.length > 0 ? "py-6" : "py-10",
          dragging
            ? "border-primary/60 bg-primary/5"
            : "border-border hover:border-primary/40 hover:bg-muted/30",
          disabled && "pointer-events-none opacity-50",
        )}
      >
        <span className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
          <UploadCloud className="h-6 w-6" />
        </span>
        <div>
          <p className="font-display text-sm font-semibold">
            {single ? "Drag and drop a file here" : "Drag and drop files here"}
          </p>
          <p className="mt-1 text-xs text-muted-foreground">or</p>
        </div>
        <Button
          type="button"
          size="sm"
          className="font-semibold"
          tabIndex={-1}
          onClick={(e) => {
            e.stopPropagation();
            browse();
          }}
        >
          Browse files
        </Button>
        <input
          ref={inputRef}
          type="file"
          multiple={!single}
          className="hidden"
          onChange={(e) => {
            add(e.target.files);
            e.target.value = "";
          }}
        />
      </div>

      {files.map((file) => (
        <div
          key={`${file.name}-${file.size}`}
          className="flex items-center gap-3 rounded-xl border border-border/60 bg-muted/30 px-4 py-2.5"
        >
          <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <FileText className="h-4 w-4" />
          </span>
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{file.name}</p>
            <p className="text-xs text-muted-foreground">{fmtBytes(file.size)}</p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="h-8 w-8"
            aria-label={`Remove ${file.name}`}
            disabled={disabled}
            onClick={() => onSelect(files.filter((f) => f !== file))}
          >
            <X className="h-4 w-4 text-muted-foreground" />
          </Button>
        </div>
      ))}
    </div>
  );
}

/**
 * Styled select for forms that deserve a polished control. `FieldSelect` (native) stays for
 * dense inline filters; this one renders its own listbox, so options can carry descriptions
 * and the open list is styled consistently across platforms.
 */
export function SelectField({
  value,
  onValueChange,
  options,
  placeholder = "Select…",
  disabled,
  id,
  className,
  icon: Icon,
}: {
  value: string;
  onValueChange: (value: string) => void;
  options: { value: string; label: string; hint?: string }[];
  placeholder?: string;
  disabled?: boolean;
  id?: string;
  className?: string;
  /** Leading icon inside the trigger, shown for both the placeholder and the selection. */
  icon?: LucideIcon;
}) {
  const selected = options.find((option) => option.value === value);

  return (
    <Select value={value} onValueChange={onValueChange} disabled={disabled}>
      <SelectTrigger
        id={id}
        className={cn(
          "h-11 gap-2 rounded-xl [&>svg]:shrink-0",
          className,
        )}
      >
        <span className="flex min-w-0 flex-1 items-center gap-2 !flex">
          {Icon && (
            <Icon className="h-4 w-4 shrink-0 text-muted-foreground" aria-hidden />
          )}
          <SelectValue placeholder={placeholder}>
            {selected && <span className="truncate">{selected.label}</span>}
          </SelectValue>
        </span>
      </SelectTrigger>
      <SelectContent className="z-[70] rounded-xl">
        {options.map((option) => (
          <SelectItem
            key={option.value}
            value={option.value}
            className="rounded-lg py-2"
          >
            <span className="block text-sm font-medium">{option.label}</span>
            {option.hint && (
              <span className="block text-xs text-muted-foreground">
                {option.hint}
              </span>
            )}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

/**
 * Date field over a calendar popover. The value is the ISO `yyyy-MM-dd` string the API's
 * `DateOnly` expects, so no timezone conversion happens on the way in or out — a date picked
 * here is the date the server stores.
 *
 * The month/year header is ours rather than react-day-picker's: its dropdown caption layers a
 * transparent native select over a visible label, so it cannot be restyled without showing
 * both. Owning the header also lets the arrows sit clear of the grid.
 */
export function DateField({
  value,
  onChange,
  placeholder = "Pick a date",
  disabled,
  id,
  fromYear = new Date().getFullYear() - 5,
  toYear = new Date().getFullYear() + 5,
}: {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  id?: string;
  fromYear?: number;
  toYear?: number;
}) {
  const [open, setOpen] = useState(false);

  const selected = value ? new Date(`${value}T12:00:00`) : undefined;

  const [month, setMonth] = useState<Date>(selected ?? new Date());

  const toIso = (date: Date) =>
    `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(
      date.getDate(),
    ).padStart(2, "0")}`;

  const monthOptions = Array.from({ length: 12 }, (_, index) => ({
    value: String(index),
    label: new Date(2000, index, 1).toLocaleString(undefined, { month: "long" }),
  }));

  const yearOptions = Array.from({ length: toYear - fromYear + 1 }, (_, index) => ({
    value: String(fromYear + index),
    label: String(fromYear + index),
  }));

  const shiftMonth = (delta: number) =>
    setMonth((current) => new Date(current.getFullYear(), current.getMonth() + delta, 1));

  return (
    <Popover
      open={open}
      onOpenChange={(next) => {
        if (next) setMonth(selected ?? new Date());
        setOpen(next);
      }}
    >
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          disabled={disabled}
          className={cn(
            "h-11 w-full justify-start rounded-xl px-3 font-normal",
            !selected && "text-muted-foreground",
          )}
        >
          <CalendarIcon className="mr-2 h-4 w-4 shrink-0 text-muted-foreground" />
          {selected
            ? selected.toLocaleDateString(undefined, {
                day: "numeric",
                month: "short",
                year: "numeric",
              })
            : placeholder}
        </Button>
      </PopoverTrigger>
      <PopoverContent
        align="start"
        sideOffset={6}
        collisionPadding={16}
        className="z-[60] w-auto rounded-xl p-0"
      >
        <div className="w-[21rem] space-y-3 p-3">
          <div className="flex items-center gap-2">
            <Button
              type="button"
              variant="outline"
              size="icon"
              aria-label="Previous month"
              className="h-9 w-9 shrink-0 rounded-lg"
              onClick={() => shiftMonth(-1)}
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>

            <SelectField
              value={String(month.getMonth())}
              onValueChange={(next) =>
                setMonth(new Date(month.getFullYear(), Number(next), 1))
              }
              options={monthOptions}
              className="h-9 min-w-0 flex-1 rounded-lg"
            />
            <SelectField
              value={String(month.getFullYear())}
              onValueChange={(next) =>
                setMonth(new Date(Number(next), month.getMonth(), 1))
              }
              options={yearOptions}
              className="h-9 w-24 shrink-0 rounded-lg"
            />

            <Button
              type="button"
              variant="outline"
              size="icon"
              aria-label="Next month"
              className="h-9 w-9 shrink-0 rounded-lg"
              onClick={() => shiftMonth(1)}
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>

          <div className="flex justify-center">
            <Calendar
              mode="single"
              month={month}
              onMonthChange={setMonth}
              selected={selected}
              fixedWeeks
              className="p-0"
              classNames={{ month_caption: "hidden", nav: "hidden" }}
              onSelect={(date) => {
                if (date) {
                  onChange(toIso(date));
                  setOpen(false);
                }
              }}
            />
          </div>
        </div>
      </PopoverContent>
    </Popover>
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
