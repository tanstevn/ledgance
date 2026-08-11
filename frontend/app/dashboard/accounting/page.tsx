"use client";

import { useEffect, useMemo, useState } from "react";
import {
  BookOpen,
  CalendarRange,
  Coins,
  Loader2,
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
  fmtDate,
} from "@/components/workspace";
import { useApiInfiniteQuery, useApiMutation } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import type { EntityCardRow } from "@/lib/accounting-types";

const PAGE_SIZE = 10;

function CreateEntityDialog({ onCreated }: { onCreated: () => void }) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [legalName, setLegalName] = useState("");
  const [baseCurrency, setBaseCurrency] = useState("USD");

  const create = useApiMutation<
    string,
    { name: string; legalName: string; baseCurrency: string }
  >("/api/accounting/entities", "post", {
    onSuccess: () => {
      toast.success("Entity created — its books are open.");
      setOpen(false);
      setName("");
      setLegalName("");
      onCreated();
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const submit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || baseCurrency.trim().length !== 3) {
      toast.error("A name and a 3-letter currency code are required.");
      return;
    }
    create.mutate({
      name: name.trim(),
      legalName: legalName.trim(),
      baseCurrency: baseCurrency.trim().toUpperCase(),
    });
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Add entity
        </Button>
      </DialogTrigger>
      <DialogContent>
        <form onSubmit={submit}>
          <DialogHeader>
            <DialogTitle>Create an accounting entity</DialogTitle>
            <DialogDescription>
              An entity is a separate set of books with its own chart of
              accounts and fiscal periods. The base currency is fixed once
              created.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="entity-name">Name</Label>
              <Input
                id="entity-name"
                placeholder="Acme Trading"
                value={name}
                onChange={(e) => setName(e.target.value)}
                autoFocus
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="entity-legal">Legal name (optional)</Label>
              <Input
                id="entity-legal"
                placeholder="Acme Trading Corp."
                value={legalName}
                onChange={(e) => setLegalName(e.target.value)}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="entity-currency">Base currency</Label>
              <Input
                id="entity-currency"
                placeholder="USD"
                maxLength={3}
                className="w-28 uppercase"
                value={baseCurrency}
                onChange={(e) => setBaseCurrency(e.target.value)}
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
                "Create entity"
              )}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function EntityCard({ entity }: { entity: EntityCardRow }) {
  return (
    <article className="flex flex-col rounded-2xl border border-border/60 bg-card p-4 transition-colors hover:border-border">
      <div className="flex items-start justify-between gap-2">
        <RecordAvatar name={entity.name} />
        <Badge
          variant="secondary"
          className="pointer-events-none gap-1 rounded-full text-[11px] font-medium"
        >
          <CalendarRange className="h-3 w-3" />
          {entity.openPeriods} open
        </Badge>
      </div>

      <h2 className="mt-4 font-display text-base font-semibold leading-tight">
        {entity.name}
      </h2>
      <p
        className="mt-0.5 truncate text-xs text-muted-foreground"
        title={entity.legalName}
      >
        {entity.legalName || "—"}
      </p>
      {entity.isArchived && (
        <Badge variant="secondary" className="mt-2 w-fit text-[11px]">
          Archived
        </Badge>
      )}

      <dl className="mt-4 space-y-1.5 border-t border-border/60 pt-3 text-xs">
        <div className="flex items-center gap-2">
          <dt className="sr-only">Base currency</dt>
          <Coins className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <dd className="text-muted-foreground">{entity.baseCurrency}</dd>
        </div>
        <div className="flex items-center gap-2">
          <dt className="sr-only">Fiscal periods</dt>
          <CalendarRange className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <dd className="text-muted-foreground">
            {entity.totalPeriods} fiscal{" "}
            {entity.totalPeriods === 1 ? "period" : "periods"}
          </dd>
        </div>
        <div className="flex items-center gap-2">
          <dt className="sr-only">Opened</dt>
          <BookOpen className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
          <dd className="text-muted-foreground">
            Opened {fmtDate(entity.createdAt)}
          </dd>
        </div>
      </dl>

      <Link
        href={`/dashboard/accounting/${entity.id}`}
        className="mt-4 border-t border-border/60 pt-3 text-xs text-primary hover:underline"
      >
        Open books
      </Link>
    </article>
  );
}

export default function AccountingEntitiesPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const platformEnabled = isPlatformEnabled(session, "accounting");
  const ready = !!session && !session.needsOnboarding && platformEnabled;

  const [search, setSearch] = useState("");
  const [appliedSearch, setAppliedSearch] = useState("");

  useEffect(() => {
    const timer = setTimeout(() => setAppliedSearch(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  const entities = useApiInfiniteQuery<EntityCardRow>(
    "/api/accounting/entities/paged",
    { pageSize: PAGE_SIZE, searchValue: appliedSearch || undefined },
    { queryKey: ["accounting-entities-paged", appliedSearch], enabled: ready },
  );

  const rows = useMemo(
    () => entities.data?.pages.flatMap((page) => page.data) ?? [],
    [entities.data],
  );

  const total = entities.data?.pages[0]?.totalResultsCount ?? 0;

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ["accounting-entities-paged"] });
    queryClient.invalidateQueries({ queryKey: ["accounting-entities"] });
  };

  if (session && !session.needsOnboarding && !platformEnabled) {
    return (
      <div className="mx-auto max-w-lg rounded-2xl border border-dashed border-border bg-card p-12 text-center">
        <BookOpen className="mx-auto h-8 w-8 text-muted-foreground" />
        <h1 className="mt-4 font-display text-lg font-semibold">
          Accounting is not activated
        </h1>
        <p className="mt-2 text-sm text-muted-foreground">
          Your organization currently uses Ledgance Audit only. The owner can
          activate Accounting — free — from plans & billing.
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
          <h1 className="font-display text-2xl font-bold tracking-tight">
            Entities
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Each entity is a separate set of double-entry books.
          </p>
        </div>
        <CreateEntityDialog onCreated={refresh} />
      </div>

      <div className="relative max-w-sm">
        <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          className="pl-9"
          placeholder="Search entities by name..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          aria-label="Search entities by name"
        />
      </div>

      {entities.isLoading || !ready ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5">
          {Array.from({ length: 5 }).map((_, index) => (
            <Skeleton key={index} className="h-60 w-full rounded-2xl" />
          ))}
        </div>
      ) : entities.isError ? (
        <ErrorCard
          title="Could not load your entities"
          errors={entities.error}
          onRetry={() => entities.refetch()}
        />
      ) : rows.length > 0 ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5">
            {rows.map((entity) => (
              <EntityCard key={entity.id} entity={entity} />
            ))}
          </div>

          <InfiniteScrollSentinel
            disabled={!entities.hasNextPage || entities.isFetchingNextPage}
            onReach={() => entities.fetchNextPage()}
          />

          <div className="flex items-center justify-center gap-2 text-xs text-muted-foreground">
            {entities.isFetchingNextPage ? (
              <>
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Loading more entities…
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
          title="No entities match that search"
          body={`Nothing found for "${appliedSearch}". Try a different name.`}
          action={
            <Button variant="outline" onClick={() => setSearch("")}>
              Clear search
            </Button>
          }
        />
      ) : (
        <EmptyCard
          icon={BookOpen}
          title="No books yet"
          body="Create your first entity to open its books — chart of accounts, fiscal periods and journal included."
          action={<CreateEntityDialog onCreated={refresh} />}
        />
      )}
    </div>
  );
}
