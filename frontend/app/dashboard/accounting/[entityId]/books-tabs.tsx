"use client";

import { useMemo, useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  BookOpen,
  CalendarRange,
  Loader2,
  Plus,
  Scale,
  Sparkles,
  Trash2,
} from "lucide-react";
import {
  ProposalCard,
  type AiProposal,
} from "@/components/ai/ai-common";
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
import {
  EmptyCard,
  ErrorCard,
  FieldSelect,
  LoadingRows,
  StatusPill,
  fmtDate,
  fmtMoney,
} from "@/components/workspace";
import { useApiAction, useApiQuery, usePaginatedQuery } from "@/hooks/query";
import {
  accountTypes,
  type AccountRow,
  type FiscalPeriodRow,
  type JournalEntryRow,
  type JournalLineInput,
} from "@/lib/accounting-types";

export function AccountsTab({ entityId }: { entityId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [type, setType] = useState("Asset");
  const [classification, setClassification] = useState("");
  const [parentId, setParentId] = useState("");

  const accounts = useApiQuery<AccountRow[]>(
    `/api/accounting/entities/${entityId}/accounts`,
    { queryKey: ["acc-accounts", entityId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Account saved.");
      setOpen(false);
      setCode("");
      setName("");
      queryClient.invalidateQueries({ queryKey: ["acc-accounts", entityId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          New account
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create an account</DialogTitle>
          <DialogDescription>
            Accounts with sub-accounts become summary accounts and stop
            accepting postings.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3 py-2 sm:grid-cols-2">
          <div className="space-y-1.5">
            <Label>Code</Label>
            <Input
              value={code}
              onChange={(e) => setCode(e.target.value)}
              placeholder="1010"
            />
          </div>
          <div className="space-y-1.5">
            <Label>Name</Label>
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Cash on hand"
            />
          </div>
          <div className="space-y-1.5">
            <Label>Type</Label>
            <FieldSelect value={type} onChange={(e) => setType(e.target.value)}>
              {accountTypes.map((value) => (
                <option key={value}>{value}</option>
              ))}
            </FieldSelect>
          </div>
          <div className="space-y-1.5">
            <Label>Classification (optional)</Label>
            <Input
              value={classification}
              onChange={(e) => setClassification(e.target.value)}
              placeholder="Current assets"
            />
          </div>
          <div className="space-y-1.5 sm:col-span-2">
            <Label>Parent account (optional)</Label>
            <FieldSelect
              value={parentId}
              onChange={(e) => setParentId(e.target.value)}
            >
              <option value="">None — top level</option>
              {(accounts.data ?? [])
                .filter((account) => account.type === type && account.isActive)
                .map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
            </FieldSelect>
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !code.trim() || !name.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/accounting/entities/${entityId}/accounts`,
                body: {
                  code: code.trim(),
                  name: name.trim(),
                  type,
                  classification,
                  parentAccountId: parentId || null,
                },
              })
            }
          >
            Create account
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (accounts.isLoading) return <LoadingRows />;
  if (accounts.isError)
    return (
      <ErrorCard errors={accounts.error} onRetry={() => accounts.refetch()} />
    );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {accounts.data && accounts.data.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-border/60 bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Name</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Normal balance</TableHead>
                <TableHead>Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {accounts.data.map((account) => (
                <TableRow key={account.id}>
                  <TableCell className="font-mono text-xs">
                    {account.parentAccountId ? "· " : ""}
                    {account.code}
                  </TableCell>
                  <TableCell className="font-medium">
                    {account.name}
                    {account.hasChildren && (
                      <span className="ml-2 text-xs text-muted-foreground">
                        (summary)
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {account.type}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {account.normalBalance}
                  </TableCell>
                  <TableCell>
                    {account.isActive ? (
                      <StatusPill value="Open" />
                    ) : (
                      <StatusPill value="Closed" />
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <EmptyCard
          icon={BookOpen}
          title="No accounts yet"
          body="Build the chart of accounts — every journal entry posts against these."
          action={addDialog}
        />
      )}
    </div>
  );
}

export function PeriodsTab({ entityId }: { entityId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");

  const periods = useApiQuery<FiscalPeriodRow[]>(
    `/api/accounting/entities/${entityId}/periods`,
    { queryKey: ["acc-periods", entityId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      setOpen(false);
      setName("");
      queryClient.invalidateQueries({ queryKey: ["acc-periods", entityId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          New period
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Open a fiscal period</DialogTitle>
          <DialogDescription>
            Journal entries can only be posted into an open period containing
            their date. Periods must not overlap.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3 py-2 sm:grid-cols-3">
          <div className="space-y-1.5">
            <Label>Name</Label>
            <Input
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="March 2026"
            />
          </div>
          <div className="space-y-1.5">
            <Label>Start</Label>
            <Input
              type="date"
              value={startDate}
              onChange={(e) => setStartDate(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>End</Label>
            <Input
              type="date"
              value={endDate}
              onChange={(e) => setEndDate(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !name.trim() || !startDate || !endDate}
            onClick={() =>
              action.mutate({
                url: `/api/accounting/entities/${entityId}/periods`,
                body: { name: name.trim(), startDate, endDate },
              })
            }
          >
            Open period
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (periods.isLoading) return <LoadingRows />;
  if (periods.isError)
    return (
      <ErrorCard errors={periods.error} onRetry={() => periods.refetch()} />
    );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {periods.data && periods.data.length > 0 ? (
        <div className="space-y-2">
          {periods.data.map((period) => (
            <div
              key={period.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border/60 bg-card px-4 py-3"
            >
              <div>
                <div className="text-sm font-semibold">{period.name}</div>
                <div className="text-xs text-muted-foreground">
                  {fmtDate(period.startDate)} – {fmtDate(period.endDate)}
                </div>
              </div>
              <div className="flex items-center gap-2">
                <StatusPill value={period.status} />
                <Button
                  size="sm"
                  variant="outline"
                  disabled={action.isPending}
                  onClick={() =>
                    action.mutate({
                      url: `/api/accounting/entities/${entityId}/periods/${period.id}/${period.status === "Open" ? "close" : "reopen"}`,
                    })
                  }
                >
                  {period.status === "Open" ? "Close" : "Reopen"}
                </Button>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={CalendarRange}
          title="No fiscal periods"
          body="Open the first period — posting requires an open period that contains the entry date."
          action={addDialog}
        />
      )}
    </div>
  );
}

const emptyLine = (): JournalLineInput => ({
  accountId: "",
  description: "",
  debit: 0,
  credit: 0,
});

function CreateEntryDialog({
  entityId,
  accounts,
  onCreated,
}: {
  entityId: string;
  accounts: AccountRow[];
  onCreated: () => void;
}) {
  const [open, setOpen] = useState(false);
  const [entryDate, setEntryDate] = useState("");
  const [memo, setMemo] = useState("");
  const [reference, setReference] = useState("");
  const [lines, setLines] = useState<JournalLineInput[]>([
    emptyLine(),
    emptyLine(),
  ]);

  const postable = accounts.filter(
    (account) => account.isActive && !account.hasChildren,
  );

  const totals = useMemo(
    () =>
      lines.reduce(
        (sum, line) => ({
          debit: sum.debit + (Number(line.debit) || 0),
          credit: sum.credit + (Number(line.credit) || 0),
        }),
        { debit: 0, credit: 0 },
      ),
    [lines],
  );
  const balanced =
    totals.debit === totals.credit && totals.debit > 0;

  const create = useApiAction<string>({
    onSuccess: () => {
      toast.success("Draft entry created.");
      setOpen(false);
      setMemo("");
      setLines([emptyLine(), emptyLine()]);
      onCreated();
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const setLine = (index: number, patch: Partial<JournalLineInput>) =>
    setLines((current) =>
      current.map((line, i) => (i === index ? { ...line, ...patch } : line)),
    );

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          New entry
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>Draft a journal entry</DialogTitle>
          <DialogDescription>
            Debits must equal credits before the entry can be created. Posting
            happens separately, into an open period.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="grid gap-3 sm:grid-cols-3">
            <div className="space-y-1.5">
              <Label>Date</Label>
              <Input
                type="date"
                value={entryDate}
                onChange={(e) => setEntryDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Memo</Label>
              <Input
                value={memo}
                onChange={(e) => setMemo(e.target.value)}
                placeholder="Cash sale"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Reference (optional)</Label>
              <Input
                value={reference}
                onChange={(e) => setReference(e.target.value)}
                placeholder="INV-001"
              />
            </div>
          </div>

          <div className="space-y-2">
            {lines.map((line, index) => (
              <div key={index} className="flex items-center gap-2">
                <FieldSelect
                  className="flex-1"
                  value={line.accountId}
                  onChange={(e) => setLine(index, { accountId: e.target.value })}
                >
                  <option value="">Account…</option>
                  {postable.map((account) => (
                    <option key={account.id} value={account.id}>
                      {account.code} — {account.name}
                    </option>
                  ))}
                </FieldSelect>
                <Input
                  className="flex-1"
                  placeholder="Line description"
                  value={line.description}
                  onChange={(e) =>
                    setLine(index, { description: e.target.value })
                  }
                />
                <Input
                  className="w-28"
                  type="number"
                  min="0"
                  step="0.01"
                  placeholder="Debit"
                  value={line.debit || ""}
                  onChange={(e) =>
                    setLine(index, {
                      debit: Number(e.target.value) || 0,
                      credit: 0,
                    })
                  }
                />
                <Input
                  className="w-28"
                  type="number"
                  min="0"
                  step="0.01"
                  placeholder="Credit"
                  value={line.credit || ""}
                  onChange={(e) =>
                    setLine(index, {
                      credit: Number(e.target.value) || 0,
                      debit: 0,
                    })
                  }
                />
                <Button
                  variant="ghost"
                  size="icon"
                  aria-label="Remove line"
                  disabled={lines.length <= 2}
                  onClick={() =>
                    setLines((current) => current.filter((_, i) => i !== index))
                  }
                >
                  <Trash2 className="h-4 w-4 text-muted-foreground" />
                </Button>
              </div>
            ))}
            <div className="flex items-center justify-between">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setLines((current) => [...current, emptyLine()])}
              >
                <Plus className="mr-2 h-4 w-4" />
                Add line
              </Button>
              <div
                className={`text-sm font-medium ${
                  balanced
                    ? "text-emerald-600 dark:text-emerald-400"
                    : "text-amber-600 dark:text-amber-400"
                }`}
              >
                Debits {fmtMoney(totals.debit)} · Credits{" "}
                {fmtMoney(totals.credit)}{" "}
                {balanced ? "— balanced" : "— must balance"}
              </div>
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={
              create.isPending ||
              !balanced ||
              !entryDate ||
              !memo.trim() ||
              lines.some((line) => !line.accountId)
            }
            onClick={() =>
              create.mutate({
                url: `/api/accounting/entities/${entityId}/journal-entries`,
                body: {
                  entryDate,
                  memo: memo.trim(),
                  reference,
                  lines,
                },
              })
            }
          >
            {create.isPending && (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            )}
            Create draft
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export function JournalTab({ entityId }: { entityId: string }) {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [explanation, setExplanation] = useState<AiProposal | null>(null);
  const [explaining, setExplaining] = useState("");

  const explain = useApiAction<AiProposal>({
    onSuccess: (result) => {
      setExplanation(result);
      setExplaining("");
    },
    onError: (errors) => {
      toast.error(errors.join(" "));
      setExplaining("");
    },
  });

  const entries = usePaginatedQuery<JournalEntryRow>(
    `/api/accounting/entities/${entityId}/journal-entries`,
    { page, pageSize: 25 },
  );

  const accounts = useApiQuery<AccountRow[]>(
    `/api/accounting/entities/${entityId}/accounts`,
    { queryKey: ["acc-accounts", entityId] },
  );

  const journalKey = `/api/accounting/entities/${entityId}/journal-entries`;

  const action = useApiAction<unknown>({
    onSuccess: () => {
      toast.success("Done.");
      queryClient.invalidateQueries({ queryKey: [journalKey] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: [journalKey] });

  const createButton = (
    <CreateEntryDialog
      entityId={entityId}
      accounts={accounts.data ?? []}
      onCreated={refresh}
    />
  );

  if (entries.isLoading) return <LoadingRows count={4} />;
  if (entries.isError)
    return <ErrorCard errors={entries.error} onRetry={() => entries.refetch()} />;

  const rows = entries.data?.data ?? [];

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{createButton}</div>
      {rows.length > 0 ? (
        <div className="overflow-x-auto rounded-2xl border border-border/60 bg-card">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>#</TableHead>
                <TableHead>Date</TableHead>
                <TableHead>Memo</TableHead>
                <TableHead className="text-right">Amount</TableHead>
                <TableHead>Status</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((entry) => (
                <TableRow key={entry.id}>
                  <TableCell className="font-mono text-xs">
                    {entry.entryNumber}
                  </TableCell>
                  <TableCell className="text-muted-foreground">
                    {fmtDate(entry.entryDate)}
                  </TableCell>
                  <TableCell>
                    <span className="font-medium">{entry.memo}</span>
                    {entry.reference && (
                      <span className="ml-2 text-xs text-muted-foreground">
                        {entry.reference}
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-right font-mono text-xs">
                    {fmtMoney(entry.totalDebits)}
                  </TableCell>
                  <TableCell>
                    <StatusPill value={entry.status} />
                  </TableCell>
                  <TableCell className="text-right">
                    <Button
                      size="sm"
                      variant="ghost"
                      className="text-muted-foreground"
                      disabled={explain.isPending}
                      onClick={() => {
                        setExplaining(entry.id);
                        explain.mutate({
                          url: `/api/accounting/ai/entities/${entityId}/entries/${entry.id}/explain`,
                        });
                      }}
                    >
                      {explaining === entry.id && explain.isPending ? (
                        <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                      ) : (
                        <Sparkles className="mr-1.5 h-3.5 w-3.5" />
                      )}
                      Explain
                    </Button>
                    {entry.status === "Draft" && (
                      <div className="flex justify-end gap-2">
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={action.isPending}
                          onClick={() =>
                            action.mutate({
                              url: `/api/accounting/entities/${entityId}/journal-entries/${entry.id}/post`,
                            })
                          }
                        >
                          Post
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          disabled={action.isPending}
                          onClick={() =>
                            action.mutate({
                              url: `/api/accounting/entities/${entityId}/journal-entries/${entry.id}`,
                              method: "delete",
                            })
                          }
                        >
                          Delete
                        </Button>
                      </div>
                    )}
                    {entry.status === "Posted" && (
                      <Button
                        size="sm"
                        variant="ghost"
                        disabled={action.isPending}
                        onClick={() =>
                          action.mutate({
                            url: `/api/accounting/entities/${entityId}/journal-entries/${entry.id}/reverse`,
                            body: { reversalDate: entry.entryDate },
                          })
                        }
                      >
                        Reverse
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <EmptyCard
          icon={Scale}
          title="No journal entries"
          body="Draft a balanced entry, then post it into an open fiscal period. Posted entries are immutable — corrections happen by reversal."
          action={createButton}
        />
      )}
      {explanation && <ProposalCard proposal={explanation} />}
      {entries.data && entries.data.totalResultsCount > 25 && (
        <div className="flex items-center justify-center gap-3">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">Page {page}</span>
          <Button
            variant="outline"
            size="sm"
            disabled={page * 25 >= entries.data.totalResultsCount}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
