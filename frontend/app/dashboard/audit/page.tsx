"use client";

import { useEffect, useMemo, useState } from "react";
import {
  ArrowRight,
  Briefcase,
  Building2,
  Globe,
  Loader2,
  Mail,
  Phone,
  Plus,
  Search,
} from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { Badge } from "@/components/ui/badge";
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
import { Skeleton } from "@/components/ui/skeleton";
import { toast } from "sonner";
import Link from "next/link";
import { useAuth } from "@/components/auth-context";
import {
  EmptyCard,
  ErrorCard,
  InfiniteScrollSentinel,
  RecordAvatar,
} from "@/components/workspace";
import { useApiInfiniteQuery, useApiMutation } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import type { ClientCardRow } from "@/lib/audit-types";

const PAGE_SIZE = 10;

interface ClientInfo {
  name: string;
  email: string;
  phone: string;
  industry: string;
  contactName: string;
}

function CreateClientDialog({ onCreated }: { onCreated: () => void }) {
  const [open, setOpen] = useState(false);
  const [info, setInfo] = useState<ClientInfo>({
    name: "",
    email: "",
    phone: "",
    industry: "",
    contactName: "",
  });

  const set = (key: keyof ClientInfo) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setInfo((prev) => ({ ...prev, [key]: e.target.value }));

  const create = useApiMutation<{ id: string }, { clientInfo: ClientInfo }>(
    "/api/audit/clients",
    "post",
    {
      onSuccess: () => {
        toast.success("Client added.");
        setOpen(false);
        setInfo({ name: "", email: "", phone: "", industry: "", contactName: "" });
        onCreated();
      },
      onError: (errors) => toast.error(errors.join(" ")),
    },
  );

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!info.name || !info.email || !info.phone || !info.industry) {
      toast.error("Name, email, phone and industry are required.");
      return;
    }
    create.mutate({ clientInfo: info });
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Add client
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>Add an audit client</DialogTitle>
            <DialogDescription>
              Clients are the organizations you audit. Engagements are created
              under a client.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-4 sm:grid-cols-2">
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="client-name">Client name</Label>
              <Input
                id="client-name"
                placeholder="Northwind Manufacturing"
                value={info.name}
                onChange={set("name")}
                autoFocus
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="client-email">Email</Label>
              <Input
                id="client-email"
                type="email"
                placeholder="finance@northwind.com"
                value={info.email}
                onChange={set("email")}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="client-phone">Phone</Label>
              <Input
                id="client-phone"
                placeholder="+1 555 0100"
                value={info.phone}
                onChange={set("phone")}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="client-industry">Industry</Label>
              <Input
                id="client-industry"
                placeholder="Manufacturing"
                value={info.industry}
                onChange={set("industry")}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="client-contact">Contact person (optional)</Label>
              <Input
                id="client-contact"
                placeholder="Alex Chen"
                value={info.contactName}
                onChange={set("contactName")}
              />
            </div>
          </div>
          <DialogFooter>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Adding...
                </>
              ) : (
                "Add client"
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function ClientCard({ client }: { client: ClientCardRow }) {
  return (
    <Link
      href={`/dashboard/audit/engagements?clientId=${client.id}`}
      aria-label={`${client.name} — view engagements`}
      className="group flex flex-col rounded-2xl border border-border/60 bg-card p-4 transition-colors hover:border-primary/40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
    >
      <div className="flex items-start justify-between gap-2">
        <RecordAvatar name={client.name} />
        <Badge
          variant="secondary"
          className="pointer-events-none gap-1 rounded-full text-[11px] font-medium"
        >
          <Briefcase className="h-3 w-3" />
          {client.activeEngagements} active
        </Badge>
      </div>

      <h2 className="mt-4 font-display text-base font-semibold leading-tight">
        {client.name}
      </h2>
      <p className="mt-0.5 truncate text-xs text-muted-foreground" title={client.industry}>
        {client.industry || "—"}
      </p>
      {client.isArchived && (
        <Badge variant="secondary" className="mt-2 w-fit text-[11px]">
          Archived
        </Badge>
      )}

      <dl className="mt-4 space-y-1.5 border-t border-border/60 pt-3 text-xs">
        <div className="flex items-center gap-2">
          <dt className="sr-only">Email</dt>
          <Mail className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <dd className="truncate text-primary" title={client.email}>
            {client.email || "—"}
          </dd>
        </div>
        <div className="flex items-center gap-2">
          <dt className="sr-only">Phone</dt>
          <Phone className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <dd className="truncate text-muted-foreground">{client.phone || "—"}</dd>
        </div>
        <div className="flex items-center gap-2">
          <dt className="sr-only">Website</dt>
          <Globe className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <dd className="truncate text-muted-foreground" title={client.website ?? ""}>
            {client.website || "—"}
          </dd>
        </div>
      </dl>

      <div className="mt-4 flex items-center justify-between gap-2 border-t border-border/60 pt-3 text-xs">
        <span className="text-muted-foreground">
          {client.totalEngagements} total{" "}
          {client.totalEngagements === 1 ? "engagement" : "engagements"}
        </span>
        <span className="flex items-center gap-1 font-medium text-primary opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
          View details
          <ArrowRight className="h-3.5 w-3.5" />
        </span>
      </div>
    </Link>
  );
}

export default function AuditClientsPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const platformEnabled = isPlatformEnabled(session, "audit");
  const ready = !!session && !session.needsOnboarding && platformEnabled;

  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");

  useEffect(() => {
    const timer = setTimeout(() => setAppliedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const clients = useApiInfiniteQuery<ClientCardRow>(
    "/api/audit/clients/paged",
    { pageSize: PAGE_SIZE, searchValue: appliedSearch || undefined },
    { queryKey: ["audit-clients-paged", appliedSearch], enabled: ready },
  );

  const rows = useMemo(
    () => clients.data?.pages.flatMap((page) => page.data) ?? [],
    [clients.data],
  );

  const total = clients.data?.pages[0]?.totalResultsCount ?? 0;

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ["audit-clients-paged"] });
    queryClient.invalidateQueries({ queryKey: ["audit-clients"] });
  };

  if (session && !session.needsOnboarding && !platformEnabled) {
    return (
      <div className="mx-auto max-w-lg rounded-2xl border border-dashed border-border bg-card p-12 text-center">
        <Building2 className="mx-auto h-8 w-8 text-muted-foreground" />
        <h1 className="mt-4 font-display text-lg font-semibold">
          Audit is not activated
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Your organization currently uses Ledgance Accounting only. The owner
          can activate Audit — free — from plans & billing.
        </p>
        <Link href="/dashboard/billing">
          <Button variant="outline" className="mt-6 font-semibold">
            Go to plans & billing
          </Button>
        </Link>
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight">Clients</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Manage your client relationships and their engagements.
          </p>
        </div>
        <CreateClientDialog onCreated={refresh} />
      </div>

      <div className="relative max-w-sm">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          className="pl-9"
          placeholder="Search clients by name..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search clients by name"
        />
      </div>

      {clients.isLoading || !ready ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <Skeleton key={index} className="h-64 w-full rounded-2xl" />
          ))}
        </div>
      ) : clients.isError ? (
        <ErrorCard
          title="Could not load your clients"
          errors={clients.error}
          onRetry={() => clients.refetch()}
        />
      ) : rows.length > 0 ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5">
            {rows.map((client) => (
              <ClientCard key={client.id} client={client} />
            ))}
          </div>

          <InfiniteScrollSentinel
            disabled={!clients.hasNextPage || clients.isFetchingNextPage}
            onReach={() => clients.fetchNextPage()}
          />

          <div className="flex items-center justify-center gap-2 text-xs text-muted-foreground">
            {clients.isFetchingNextPage ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Loading more clients…
              </>
            ) : (
              <span>
                Showing {rows.length} of {total}
              </span>
            )}
          </div>
        </>
      ) : appliedSearch ? (
        <EmptyCard
          icon={Search}
          title="No clients match that search"
          body={`Nothing found for "${appliedSearch}". Try a different name.`}
          action={
            <Button variant="outline" onClick={() => setSearch("")}>
              Clear search
            </Button>
          }
        />
      ) : (
        <EmptyCard
          icon={Building2}
          title="No clients yet"
          body="Add the first organization you audit — engagements, teams and working papers start from here."
          action={<CreateClientDialog onCreated={refresh} />}
        />
      )}
    </div>
  );
}
