"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  BarChart3,
  CheckCircle2,
  FileText,
  History,
  Loader2,
  Plus,
  Upload,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
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
  fmtBytes,
  fmtDate,
  fmtMoney,
} from "@/components/workspace";
import { useApiAction, useApiQuery, useApiUpload } from "@/hooks/query";
import type {
  AccountRow,
  ActivityRow,
  BalanceSheetView,
  DocumentRow,
  FiscalPeriodRow,
  IncomeStatementView,
  ReconciliationRow,
  ReportLineView,
  TrialBalanceView,
} from "@/lib/accounting-types";

function ReportSection({
  title,
  lines,
  total,
  totalLabel,
}: {
  title: string;
  lines: ReportLineView[];
  total: number;
  totalLabel: string;
}) {
  return (
    <div>
      <h4 className="text-sm font-semibold">{title}</h4>
      <div className="mt-2 space-y-1">
        {lines.length === 0 ? (
          <p className="text-sm text-muted-foreground">No activity.</p>
        ) : (
          lines.map((line) => (
            <div
              key={line.accountId}
              className="flex justify-between text-sm"
            >
              <span className="text-muted-foreground">
                <span className="mr-2 font-mono text-xs">{line.accountCode}</span>
                {line.accountName}
              </span>
              <span className="font-mono text-xs">{fmtMoney(line.amount)}</span>
            </div>
          ))
        )}
        <div className="flex justify-between border-t border-border/60 pt-1.5 text-sm font-semibold">
          <span>{totalLabel}</span>
          <span className="font-mono text-xs">{fmtMoney(total)}</span>
        </div>
      </div>
    </div>
  );
}

export function ReportsTab({ entityId }: { entityId: string }) {
  const [periodId, setPeriodId] = useState("");

  const periods = useApiQuery<FiscalPeriodRow[]>(
    `/api/accounting/entities/${entityId}/periods`,
    { queryKey: ["acc-periods", entityId] },
  );

  const enabled = !!periodId;

  const trialBalance = useApiQuery<TrialBalanceView>(
    `/api/accounting/entities/${entityId}/trial-balance?periodId=${periodId}`,
    { queryKey: ["acc-tb", entityId, periodId], enabled },
  );

  const income = useApiQuery<IncomeStatementView>(
    `/api/accounting/entities/${entityId}/reports/income-statement?periodId=${periodId}`,
    { queryKey: ["acc-is", entityId, periodId], enabled },
  );

  const balance = useApiQuery<BalanceSheetView>(
    `/api/accounting/entities/${entityId}/reports/balance-sheet?periodId=${periodId}`,
    { queryKey: ["acc-bs", entityId, periodId], enabled },
  );

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-56 space-y-1.5">
          <Label>Fiscal period</Label>
          <FieldSelect
            value={periodId}
            onChange={(e) => setPeriodId(e.target.value)}
          >
            <option value="">Select a period…</option>
            {(periods.data ?? []).map((period) => (
              <option key={period.id} value={period.id}>
                {period.name}
              </option>
            ))}
          </FieldSelect>
        </div>
      </div>

      {!periodId ? (
        <EmptyCard
          icon={BarChart3}
          title="Pick a period"
          body="The trial balance, income statement and balance sheet are derived live from posted entries — choose a fiscal period to view them."
        />
      ) : trialBalance.isLoading || income.isLoading || balance.isLoading ? (
        <LoadingRows count={4} />
      ) : trialBalance.isError ? (
        <ErrorCard
          errors={trialBalance.error}
          onRetry={() => trialBalance.refetch()}
        />
      ) : (
        <div className="grid gap-6 lg:grid-cols-2">
          <Card className="border-border/60 lg:col-span-2">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
              <CardTitle className="font-display text-base font-semibold">
                Trial balance · as of {fmtDate(trialBalance.data?.asOf)}
              </CardTitle>
              {trialBalance.data?.isBalanced ? (
                <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
                  <CheckCircle2 className="h-4 w-4" />
                  Balanced
                </span>
              ) : (
                <StatusPill value="Critical" />
              )}
            </CardHeader>
            <CardContent className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Code</TableHead>
                    <TableHead>Account</TableHead>
                    <TableHead>Type</TableHead>
                    <TableHead className="text-right">Debit</TableHead>
                    <TableHead className="text-right">Credit</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {(trialBalance.data?.rows ?? []).map((row) => (
                    <TableRow key={row.accountId}>
                      <TableCell className="font-mono text-xs">
                        {row.accountCode}
                      </TableCell>
                      <TableCell>{row.accountName}</TableCell>
                      <TableCell className="text-muted-foreground">
                        {row.type}
                      </TableCell>
                      <TableCell className="text-right font-mono text-xs">
                        {row.debitBalance ? fmtMoney(row.debitBalance) : ""}
                      </TableCell>
                      <TableCell className="text-right font-mono text-xs">
                        {row.creditBalance ? fmtMoney(row.creditBalance) : ""}
                      </TableCell>
                    </TableRow>
                  ))}
                  <TableRow className="font-semibold">
                    <TableCell colSpan={3}>Totals</TableCell>
                    <TableCell className="text-right font-mono text-xs">
                      {fmtMoney(trialBalance.data?.totalDebitBalances ?? 0)}
                    </TableCell>
                    <TableCell className="text-right font-mono text-xs">
                      {fmtMoney(trialBalance.data?.totalCreditBalances ?? 0)}
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </CardContent>
          </Card>

          {income.data && (
            <Card className="border-border/60">
              <CardHeader className="pb-3">
                <CardTitle className="font-display text-base font-semibold">
                  Income statement · {income.data.periodName}
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <ReportSection
                  title="Revenue"
                  lines={income.data.revenue}
                  total={income.data.totalRevenue}
                  totalLabel="Total revenue"
                />
                <ReportSection
                  title="Expenses"
                  lines={income.data.expenses}
                  total={income.data.totalExpenses}
                  totalLabel="Total expenses"
                />
                <div className="flex justify-between rounded-lg bg-muted/40 px-3 py-2 text-sm font-bold">
                  <span>Net income</span>
                  <span className="font-mono text-xs">
                    {fmtMoney(income.data.netIncome)}
                  </span>
                </div>
              </CardContent>
            </Card>
          )}

          {balance.data && (
            <Card className="border-border/60">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
                <CardTitle className="font-display text-base font-semibold">
                  Balance sheet · as of {fmtDate(balance.data.asOf)}
                </CardTitle>
                {balance.data.isBalanced && (
                  <span className="flex items-center gap-1.5 text-xs font-medium text-emerald-600 dark:text-emerald-400">
                    <CheckCircle2 className="h-4 w-4" />
                    Ties
                  </span>
                )}
              </CardHeader>
              <CardContent className="space-y-4">
                <ReportSection
                  title="Assets"
                  lines={balance.data.assets}
                  total={balance.data.totalAssets}
                  totalLabel="Total assets"
                />
                <ReportSection
                  title="Liabilities"
                  lines={balance.data.liabilities}
                  total={balance.data.totalLiabilities}
                  totalLabel="Total liabilities"
                />
                <ReportSection
                  title="Equity"
                  lines={balance.data.equity}
                  total={balance.data.totalEquity}
                  totalLabel="Total equity"
                />
                <div className="flex justify-between text-xs text-muted-foreground">
                  <span>Current earnings (life to date)</span>
                  <span className="font-mono">
                    {fmtMoney(balance.data.currentEarnings)}
                  </span>
                </div>
              </CardContent>
            </Card>
          )}
        </div>
      )}
    </div>
  );
}

export function ReconciliationsTab({ entityId }: { entityId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [accountId, setAccountId] = useState("");
  const [statementDate, setStatementDate] = useState("");
  const [statementBalance, setStatementBalance] = useState("");

  const reconciliations = useApiQuery<ReconciliationRow[]>(
    `/api/accounting/entities/${entityId}/reconciliations`,
    { queryKey: ["acc-recs", entityId] },
  );

  const accounts = useApiQuery<AccountRow[]>(
    `/api/accounting/entities/${entityId}/accounts`,
    { queryKey: ["acc-accounts", entityId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Reconciliation started.");
      setOpen(false);
      queryClient.invalidateQueries({ queryKey: ["acc-recs", entityId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const startDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Start reconciliation
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reconcile an account</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="space-y-1.5">
            <Label>Account</Label>
            <FieldSelect
              value={accountId}
              onChange={(e) => setAccountId(e.target.value)}
            >
              <option value="">Select…</option>
              {(accounts.data ?? [])
                .filter((account) => account.isActive && !account.hasChildren)
                .map((account) => (
                  <option key={account.id} value={account.id}>
                    {account.code} — {account.name}
                  </option>
                ))}
            </FieldSelect>
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Statement date</Label>
              <Input
                type="date"
                value={statementDate}
                onChange={(e) => setStatementDate(e.target.value)}
              />
            </div>
            <div className="space-y-1.5">
              <Label>Statement balance</Label>
              <Input
                type="number"
                step="0.01"
                value={statementBalance}
                onChange={(e) => setStatementBalance(e.target.value)}
              />
            </div>
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !accountId || !statementDate}
            onClick={() =>
              action.mutate({
                url: `/api/accounting/entities/${entityId}/reconciliations`,
                body: {
                  accountId,
                  statementDate,
                  statementBalance: Number(statementBalance) || 0,
                },
              })
            }
          >
            Start
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (reconciliations.isLoading) return <LoadingRows />;
  if (reconciliations.isError)
    return (
      <ErrorCard
        errors={reconciliations.error}
        onRetry={() => reconciliations.refetch()}
      />
    );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{startDialog}</div>
      {reconciliations.data && reconciliations.data.length > 0 ? (
        <div className="space-y-2">
          {reconciliations.data.map((rec) => (
            <div
              key={rec.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border/60 bg-card px-4 py-3"
            >
              <div>
                <div className="text-sm font-semibold">
                  {rec.accountCode} — {rec.accountName}
                </div>
                <div className="text-xs text-muted-foreground">
                  Statement {fmtDate(rec.statementDate)} ·{" "}
                  {fmtMoney(rec.statementBalance)}
                  {rec.difference !== null &&
                    ` · difference ${fmtMoney(rec.difference)}`}
                </div>
              </div>
              <StatusPill value={rec.status} />
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={CheckCircle2}
          title="No reconciliations yet"
          body="Reconcile any account against its external statement — clearing lines until the difference is zero or explained."
          action={startDialog}
        />
      )}
    </div>
  );
}

export function DocumentsTab({ entityId }: { entityId: string }) {
  const queryClient = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  const [description, setDescription] = useState("");

  const documents = useApiQuery<DocumentRow[]>(
    `/api/accounting/entities/${entityId}/documents`,
    { queryKey: ["acc-docs", entityId] },
  );

  const upload = useApiUpload({
    onSuccess: () => {
      toast.success("Document uploaded.");
      setFile(null);
      setDescription("");
      queryClient.invalidateQueries({ queryKey: ["acc-docs", entityId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  if (documents.isLoading) return <LoadingRows />;
  if (documents.isError)
    return (
      <ErrorCard errors={documents.error} onRetry={() => documents.refetch()} />
    );

  return (
    <div className="space-y-4">
      <Card className="border-border/60">
        <CardHeader className="pb-3">
          <CardTitle className="font-display text-base font-semibold">
            Upload a source document
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-wrap items-end gap-3">
          <div className="min-w-60 space-y-1.5">
            <Label>File</Label>
            <Input
              type="file"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
            />
          </div>
          <div className="min-w-60 flex-1 space-y-1.5">
            <Label>Description</Label>
            <Input
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="March supplier invoice"
            />
          </div>
          <Button
            className="font-semibold"
            disabled={!file || upload.isPending}
            onClick={() => {
              if (!file) return;
              const form = new FormData();
              form.append("file", file);
              form.append("description", description);
              upload.mutate({
                url: `/api/accounting/entities/${entityId}/documents`,
                form,
              });
            }}
          >
            {upload.isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <Upload className="mr-2 h-4 w-4" />
            )}
            Upload
          </Button>
        </CardContent>
      </Card>

      {documents.data && documents.data.length > 0 ? (
        <div className="space-y-2">
          {documents.data.map((doc) => (
            <div
              key={doc.id}
              className="flex flex-wrap items-center justify-between gap-2 rounded-xl border border-border/60 bg-card px-4 py-3"
            >
              <div>
                <div className="text-sm font-semibold">{doc.fileName}</div>
                <div className="text-xs text-muted-foreground">
                  {doc.description || "No description"} ·{" "}
                  {fmtBytes(doc.sizeBytes)} · {fmtDate(doc.uploadedAt)}
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={FileText}
          title="No documents yet"
          body="Attach invoices, receipts and statements to keep the books audit-ready."
        />
      )}
    </div>
  );
}

export function ActivityTab({ entityId }: { entityId: string }) {
  const activity = useApiQuery<ActivityRow[]>(
    `/api/accounting/entities/${entityId}/activity`,
    { queryKey: ["acc-activity", entityId] },
  );

  if (activity.isLoading) return <LoadingRows />;
  if (activity.isError)
    return (
      <ErrorCard errors={activity.error} onRetry={() => activity.refetch()} />
    );

  return activity.data && activity.data.length > 0 ? (
    <div className="space-y-2">
      {activity.data.map((entry) => (
        <div
          key={entry.id}
          className="flex items-start gap-3 rounded-xl border border-border/60 bg-card px-4 py-3"
        >
          <History className="mt-0.5 h-4 w-4 flex-shrink-0 text-muted-foreground" />
          <div className="min-w-0 flex-1">
            <p className="text-sm">{entry.summary}</p>
            <p className="mt-0.5 text-xs text-muted-foreground">
              {entry.actorEmail} · {new Date(entry.occurredAt).toLocaleString()}
            </p>
          </div>
        </div>
      ))}
    </div>
  ) : (
    <EmptyCard
      icon={History}
      title="No activity yet"
      body="Every material change to these books is recorded here, append-only."
    />
  );
}
