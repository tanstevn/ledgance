"use client";

import { useState } from "react";
import Link from "next/link";
import { useQueryClient } from "@tanstack/react-query";
import { ClipboardCheck, Loader2, Plus } from "lucide-react";
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
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { toast } from "sonner";
import { useAuth } from "@/components/auth-context";
import {
  EmptyCard,
  ErrorCard,
  FieldSelect,
  LoadingRows,
  StatusPill,
  fmtDate,
} from "@/components/workspace";
import { useApiMutation, useApiQuery } from "@/hooks/query";
import { isPlatformEnabled, useSession } from "@/hooks/session";
import {
  engagementTypes,
  type ClientRow,
  type EngagementListRow,
} from "@/lib/audit-types";

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
                    {value.replace(/([a-z])([A-Z])/g, "$1 $2")}
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

export default function EngagementsPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const ready =
    !!session && !session.needsOnboarding && isPlatformEnabled(session, "audit");

  const engagements = useApiQuery<EngagementListRow[]>("/api/audit/engagements", {
    queryKey: ["audit-engagements"],
    enabled: ready,
  });

  const clients = useApiQuery<ClientRow[]>("/api/audit/clients", {
    queryKey: ["audit-clients"],
    enabled: ready,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["audit-engagements"] });

  const createButton = (
    <CreateEngagementDialog clients={clients.data ?? []} onCreated={refresh} />
  );

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight">
            Engagements
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Every audit you run — planning, fieldwork, review and reporting live
            inside the engagement.
          </p>
        </div>
        {createButton}
      </div>

      {engagements.isLoading || !ready ? (
        <LoadingRows count={4} />
      ) : engagements.isError ? (
        <ErrorCard
          title="Could not load engagements"
          errors={engagements.error}
          onRetry={() => engagements.refetch()}
        />
      ) : engagements.data && engagements.data.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-border/60 bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Engagement</TableHead>
                <TableHead>Client</TableHead>
                <TableHead>Period</TableHead>
                <TableHead>Budget</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {engagements.data.map((engagement) => (
                <TableRow key={engagement.id}>
                  <TableCell>
                    <Link
                      href={`/dashboard/audit/engagements/${engagement.id}`}
                      className="font-medium text-primary hover:underline"
                    >
                      {engagement.name}
                    </Link>
                    <div className="text-xs text-muted-foreground">
                      {engagement.type.replace(/([a-z])([A-Z])/g, "$1 $2")}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {engagement.clientName}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {fmtDate(engagement.periodStart)} –{" "}
                    {fmtDate(engagement.periodEnd)}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {engagement.budgetHours}h
                  </TableCell>
                  <TableCell>
                    <StatusPill value={engagement.status} />
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
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
