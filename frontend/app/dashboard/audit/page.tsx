"use client";

import { useState } from "react";
import {
  AlertCircle,
  Building2,
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

interface ClientRow {
  id: string;
  name: string;
  email: string;
  phone: string;
  industry: string;
  contactName: string;
  isArchived: boolean;
  createdAt: string;
}

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
          New client
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

export default function AuditClientsPage() {
  const { user } = useAuth();
  const { data: session } = useSession(!!user);
  const queryClient = useQueryClient();
  const platformEnabled = isPlatformEnabled(session, "audit");
  const ready = !!session && !session.needsOnboarding && platformEnabled;

  const clients = useApiQuery<ClientRow[]>("/api/audit/clients", {
    queryKey: ["audit-clients"],
    enabled: ready,
  });

  const refresh = () =>
    queryClient.invalidateQueries({ queryKey: ["audit-clients"] });

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
    <div className="mx-auto max-w-5xl space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 className="font-display text-2xl font-bold tracking-tight">
            Audit clients
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">
            The organizations you audit. Engagements live under a client.
          </p>
        </div>
        <CreateClientDialog onCreated={refresh} />
      </div>

      {clients.isLoading || !ready ? (
        <div className="space-y-3">
          {[1, 2, 3].map((i) => (
            <Skeleton key={i} className="h-14 w-full rounded-xl" />
          ))}
        </div>
      ) : clients.isError ? (
        <div className="rounded-2xl border border-border/60 bg-card p-10 text-center">
          <AlertCircle className="mx-auto h-8 w-8 text-destructive" />
          <h2 className="mt-4 font-display text-lg font-semibold">
            Could not load your clients
          </h2>
          <p className="mt-2 text-sm text-muted-foreground">
            {clients.error?.join(" ") ?? "Something went wrong."}
          </p>
          <Button
            variant="outline"
            className="mt-6"
            onClick={() => clients.refetch()}
          >
            <RefreshCw className="mr-2 h-4 w-4" />
            Retry
          </Button>
        </div>
      ) : clients.data && clients.data.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-border/60 bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Industry</TableHead>
                <TableHead>Contact</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {clients.data.map((client) => (
                <TableRow key={client.id}>
                  <TableCell>
                    <div className="font-medium">{client.name}</div>
                    <div className="text-xs text-muted-foreground">
                      {client.email}
                    </div>
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {client.industry}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {client.contactName || "—"}
                  </TableCell>
                  <TableCell>
                    {client.isArchived ? (
                      <Badge variant="secondary">Archived</Badge>
                    ) : (
                      <Badge className="bg-sky-100 text-sky-700 hover:bg-sky-100 dark:bg-sky-950/40 dark:text-sky-400">
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
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-sky-50 dark:bg-sky-950/40">
            <Building2 className="h-6 w-6 text-sky-500" />
          </div>
          <h2 className="mt-4 font-display text-lg font-semibold">
            No clients yet
          </h2>
          <p className="mx-auto mt-2 max-w-sm text-sm text-muted-foreground">
            Add the first organization you audit — engagements, teams and
            working papers start from here.
          </p>
          <div className="mt-6 flex justify-center">
            <CreateClientDialog onCreated={refresh} />
          </div>
        </div>
      )}
    </div>
  );
}
