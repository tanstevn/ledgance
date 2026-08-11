"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { CalendarDays, ClipboardCheck, Clock, Loader2, Plus, Search } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import {
  EmptyCard,
  ErrorCard,
  FieldSelect,
  LoadingRows,
  Pagination,
  ProgressTrack,
  RecordAvatar,
  StatusPill,
  fmtDate,
} from "@/components/workspace";
import { usePaginatedQuery, useApiMutation, useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import {
  engagementStatuses,
  engagementTypes,
  type ClientRow,
  type EngagementListRow,
} from "@/lib/audit-types";

const PAGE_SIZE = 10;

const stageOf = (status: string) => {
  const index = engagementStatuses.indexOf(
    status as (typeof engagementStatuses)[number],
  );
  return index < 0 ? 0 : index + 1;
};

const spaced = (value: string) => value.replace(/([a-z])([A-Z])/g, "$1 $2");

function CreateEngagementDialog({
  clients,
  onCreated,
}: {
  clients: ClientRow[];
  onCreated: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [clientId, setClientId] = useState("");
  const [name, setName] = useState("");
  const [type, setType] = useState<string>("FinancialStatement");
  const [periodStart, setPeriodStart] = useState("");
  const [periodEnd, setPeriodEnd] = useState("");
  const [budgetHours, setBudgetHours] = useState("100");

  const create = useApiMutation<{ id: string }, object>(
    "/api/audit/engagements",
    "post",
    {
      onSuccess: () => {
        toast.success("Engagement created.");
        setOpen(false);
        setName("");
        onCreated();
      },
      onError: (errors) => toast.error(errors.join(" ")),
    },
  );

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!clientId || !name.trim() || !periodStart || !periodEnd) {
      toast.error("Client, name and period dates are required.");
      return;
    }
    create.mutate({
      clientId,
      name: name.trim(),
      type,
      periodStart,
      periodEnd,
      budgetHours: Number(budgetHours) || 0,
    });
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          New engagement
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>Create an engagement</DialogTitle>
            <DialogDescription>
              An engagement is the audit of one client for one period. You are
              added to its team automatically.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="eng-client">Client</Label>
              <FieldSelect
                id="eng-client"
                value={clientId}
                onChange={(e) => setClientId(e.target.value)}
              >
                <option value="">Select a client…</option>
                {clients
                  .filter((client) => !client.isArchived)
                  .map((client) => (
                    <option key={client.id} value={client.id}>
                      {client.name}
                    </option>
                  ))}
              </FieldSelect>
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="eng-name">Engagement name</Label>
              <Input
                id="eng-name"
                placeholder="FY2026 Financial Statement Audit"
                value={name}
                onChange={(e) => setName(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="eng-type">Type</Label>
              <FieldSelect
                id="eng-type"
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
              <Label htmlFor="eng-budget">Budget hours</Label>
              <Input
                id="eng-budget"
                type="number"
                min="0"
                value={budgetHours}
                onChange={(e) => setBudgetHours(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="eng-start">Period start</Label>
              <Input
                id="eng-start"
                type="date"
                value={periodStart}
                onChange={(e) => setPeriodStart(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="eng-end">Period end</Label>
              <Input
                id="eng-end"
                type="date"
                value={periodEnd}
                onChange={(e) => setPeriodEnd(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Creating...
                </>
              ) : (
                "Create engagement"
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function EngagementRow({ engagement }: { engagement: EngagementListRow }) {
  const stage = stageOf(engagement.status);

  return (
    <Link
      href={`/dashboard/audit/engagements/${engagement.id}`}
      className="flex flex-wrap items-center gap-4 rounded-2xl border border-border/60 bg-card p-4 transition-colors hover:border-primary/40"
    >
      <RecordAvatar name={engagement.clientName || engagement.name} />

      <div className="min-w-56 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="font-display text-sm font-semibold">{engagement.name}</h2>
          <StatusPill value={engagement.status} />
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
          <span>{engagement.clientName || "—"}</span>
          <span className="flex items-center gap-1">
            <CalendarDays className="h-3.5 w-3.5" />
            {fmtDate(engagement.periodStart)} — {fmtDate(engagement.periodEnd)}
          </span>
          <span className="flex items-center gap-1">
            <Clock className="h-3.5 w-3.5" />
            {engagement.budgetHours}h budget
          </span>
          <span>{spaced(engagement.type)}</span>
        </div>
      </div>

      <div className="w-full sm:w-56">
        <div className="flex items-center justify-between text-xs">
          <span className="text-muted-foreground">Stage</span>
          <span className="font-semibold">{stage} of 5</span>
        </div>
        <ProgressTrack value={stage} max={5} className="mt-1.5" />
      </div>
    </Link>
  );
}

function EngagementsView() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const ready =
    !!session && !session.needsOnboarding && isPlatformEnabled(session, "audit");

  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [clientId, setClientId] = useState(searchParams.get("clientId") ?? "");
  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");

  // A new search term invalidates the page the user was on, as the other filters do.
  useEffect(() => {
    const timer = setTimeout(() => {
      setAppliedSearch(search.trim());
      setPage(1);
    }, 300);

    return () => clearTimeout(timer);
  }, [search]);

  const params = useMemo(
    () => ({
      page,
      pageSize: PAGE_SIZE,
      status: status || undefined,
      clientId: clientId || undefined,
      searchValue: appliedSearch || undefined,
    }),
    [page, status, clientId, appliedSearch],
  );

  const engagements = usePaginatedQuery<EngagementListRow>(
    "/api/audit/engagements/paged",
    params,
    { enabled: ready },
  );

  const clients = useApiQuery<ClientRow[]>("/api/audit/clients", {
    queryKey: ["audit-clients"],
    enabled: ready,
  });

  const refresh = () =>
    queryClient.invalidateQueries({
      queryKey: ["/api/audit/engagements/paged"],
    });

  const rows = engagements.data?.data ?? [];
  const filtered = !!(status || clientId || appliedSearch);

  const createButton = (
    <CreateEngagementDialog clients={clients.data ?? []} onCreated={refresh} />
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight">
            Engagements
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Track and manage all audit engagements across your firm.
          </p>
        </div>
        {createButton}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="relative min-w-56 flex-1 sm:max-w-xs">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            className="pl-9"
            placeholder="Search engagements..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            aria-label="Search engagements by name"
          />
        </div>

        <FieldSelect
          className="w-44"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
          aria-label="Filter by status"
        >
          <option value="">All statuses</option>
          {engagementStatuses.map((value) => (
            <option key={value} value={value}>
              {spaced(value)}
            </option>
          ))}
        </FieldSelect>

        <FieldSelect
          className="w-52"
          value={clientId}
          onChange={(e) => {
            setClientId(e.target.value);
            setPage(1);
          }}
          aria-label="Filter by client"
        >
          <option value="">All clients</option>
          {(clients.data ?? []).map((client) => (
            <option key={client.id} value={client.id}>
              {client.name}
            </option>
          ))}
        </FieldSelect>

        {filtered && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => {
              setStatus("");
              setClientId("");
              setSearch("");
            }}
          >
            Clear filters
          </Button>
        )}
      </div>

      {engagements.isLoading || !ready ? (
        <LoadingRows count={5} />
      ) : engagements.isError ? (
        <ErrorCard
          title="Could not load engagements"
          errors={engagements.error}
          onRetry={() => engagements.refetch()}
        />
      ) : rows.length > 0 ? (
        <>
          <div className="space-y-3">
            {rows.map((engagement) => (
              <EngagementRow key={engagement.id} engagement={engagement} />
            ))}
          </div>

          <Pagination
            page={engagements.data?.pageNumber ?? 1}
            totalPages={engagements.data?.totalPages ?? 1}
            totalResults={engagements.data?.totalResultsCount}
            onChange={setPage}
          />
        </>
      ) : filtered ? (
        <EmptyCard
          icon={Search}
          title="No engagements match these filters"
          body="Try a different status, client or search term."
          action={
            <Button
              variant="outline"
              onClick={() => {
                setStatus("");
                setClientId("");
                setSearch("");
              }}
            >
              Clear filters
            </Button>
          }
        />
      ) : (
        <EmptyCard
          icon={ClipboardCheck}
          title="No engagements yet"
          body={
            clients.data && clients.data.length > 0
              ? "Create your first engagement to start planning the audit."
              : "Add a client first — engagements are created under a client."
          }
          action={
            clients.data && clients.data.length > 0 ? (
              createButton
            ) : (
              <Link href="/dashboard/audit">
                <Button variant="outline" className="font-semibold">
                  Go to clients
                </Button>
              </Link>
            )
          }
        />
      )}
    </div>
  );
}

/** The client filter can arrive as ?clientId=…, so the view reads search params. */
export default function EngagementsPage() {
  return (
    <Suspense fallback={<LoadingRows count={5} />}>
      <EngagementsView />
    </Suspense>
  );
}
