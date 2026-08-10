"use client";

import { useState } from "react";
import {
  AlertCircle,
  BookOpen,
  Loader2,
  Plus,
  RefreshCw,
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { toast } from "sonner";
import Link from "next/link";
import { useAuth } from "@/components/auth-context";
import { useApiMutation, useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";

interface EntityRow {
  id: string;
  name: string;
  legalName: string;
  baseCurrency: string;
  isArchived: boolean;
  createdAt: string;
}

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
          New entity
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

export default function AccountingEntitiesPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const platformEnabled = isPlatformEnabled(session, "accounting");
  const ready = !!session && !session.needsOnboarding && platformEnabled;

  const entities = useApiQuery<EntityRow[]>("/api/accounting/entities", {
    queryKey: ["accounting-entities"],
    enabled: ready,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["accounting-entities"] });

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
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight">
            Entities & books
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Each entity is a separate set of double-entry books.
          </p>
        </div>
        <CreateEntityDialog onCreated={refresh} />
      </div>

      {entities.isLoading || !ready ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-14 w-full rounded-xl" />
          ))}
        </div>
      ) : entities.isError ? (
        <div className="rounded-2xl border border-border/60 bg-card p-10 text-center">
          <AlertCircle className="mx-auto h-8 w-8 text-destructive" />
          <h2 className="mt-4 font-display text-lg font-semibold">
            Could not load your entities
          </h2>
          <p className="mt-2 text-sm text-muted-foreground">
            {entities.error?.join(" ") ?? "Something went wrong."}
          </p>
          <Button
            variant="outline"
            className="mt-6"
            onClick={() => entities.refetch()}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Retry
          </Button>
        </div>
      ) : entities.data && entities.data.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-border/60 bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Legal name</TableHead>
                <TableHead>Currency</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {entities.data.map((entity) => (
                <TableRow key={entity.id}>
                  <TableCell>
                    <Link
                      href={`/dashboard/accounting/${entity.id}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {entity.name}
                    </Link>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {entity.legalName || "—"}
                  </TableCell>
                  <TableCell>{entity.baseCurrency}</TableCell>
                  <TableCell>
                    {entity.isArchived ? (
                      <Badge variant="secondary">Archived</Badge>
                    ) : (
                      <Badge className="bg-emerald-100 text-emerald-700 hover:bg-emerald-100 dark:bg-emerald-950/40 dark:text-emerald-400">
                        Active
                      </Badge>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <div className="rounded-2xl border border-dashed border-border bg-card p-12 text-center">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-emerald-50 dark:bg-emerald-950/40">
            <BookOpen className="h-6 w-6 text-emerald-500" />
          </div>
          <h2 className="mt-4 font-display text-lg font-semibold">
            No books yet
          </h2>
          <p className="mx-auto mt-2 max-w-sm text-sm text-muted-foreground">
            Create your first entity to open its books — chart of accounts,
            fiscal periods and journal included.
          </p>
          <div className="mt-6 flex justify-center">
            <CreateEntityDialog onCreated={refresh} />
          </div>
        </div>
      )}
    </div>
  );
}
