"use client";

import { useState } from "react";
import { useQueryClient } from "@tanstack/react-query";
import {
  AlertTriangle,
  FileSpreadsheet,
  ListChecks,
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
import { Textarea } from "@/components/ui/textarea";
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
import { useApiAction, useApiQuery } from "@/hooks/query";
import {
  riskRatings,
  type ProcedureRow,
  type RiskRow,
  type TrialBalanceView,
} from "@/lib/audit-types";

export function RisksTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [assertions, setAssertions] = useState("");
  const [likelihood, setLikelihood] = useState("Medium");
  const [impact, setImpact] = useState("Medium");
  const [response, setResponse] = useState("");

  const risks = useApiQuery<RiskRow[]>(
    `/api/audit/engagements/${engagementId}/risks`,
    { queryKey: ["audit-risks", engagementId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Risk recorded.");
      setOpen(false);
      setTitle("");
      setDescription("");
      queryClient.invalidateQueries({ queryKey: ["audit-risks", engagementId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Add risk
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Record a risk of material misstatement</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="space-y-1.5">
            <Label>Title</Label>
            <Input
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Revenue cut-off around year end"
            />
          </div>
          <div className="space-y-1.5">
            <Label>Description</Label>
            <Textarea
              rows={2}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>Assertions affected</Label>
            <Input
              value={assertions}
              onChange={(e) => setAssertions(e.target.value)}
              placeholder="Occurrence, cut-off"
            />
          </div>
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Likelihood</Label>
              <FieldSelect
                value={likelihood}
                onChange={(e) => setLikelihood(e.target.value)}
              >
                {riskRatings.map((rating) => (
                  <option key={rating}>{rating}</option>
                ))}
              </FieldSelect>
            </div>
            <div className="space-y-1.5">
              <Label>Impact</Label>
              <FieldSelect
                value={impact}
                onChange={(e) => setImpact(e.target.value)}
              >
                {riskRatings.map((rating) => (
                  <option key={rating}>{rating}</option>
                ))}
              </FieldSelect>
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Planned response</Label>
            <Textarea
              rows={2}
              value={response}
              onChange={(e) => setResponse(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !title.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagementId}/risks`,
                body: {
                  title: title.trim(),
                  description,
                  assertions,
                  likelihood,
                  impact,
                  plannedResponse: response,
                },
              })
            }
          >
            {action.isPending && (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            )}
            Record risk
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (risks.isLoading) return <LoadingRows />;
  if (risks.isError)
    return (
      <ErrorCard errors={risks.error} onRetry={() => risks.refetch()} />
    );

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {risks.data && risks.data.length > 0 ? (
        <div className="space-y-3">
          {risks.data.map((risk) => (
            <div
              key={risk.id}
              className="rounded-xl border border-border/60 bg-card p-4"
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="font-semibold">{risk.title}</div>
                <div className="flex items-center gap-2">
                  <span className="text-xs text-muted-foreground">
                    {risk.likelihood} likelihood · {risk.impact} impact
                  </span>
                  <StatusPill value={risk.level} />
                </div>
              </div>
              <p className="mt-1.5 text-sm text-muted-foreground">
                {risk.description}
              </p>
              <div className="mt-2 grid gap-2 text-xs text-muted-foreground sm:grid-cols-2">
                <div>
                  <span className="font-medium text-foreground">Assertions:</span>{" "}
                  {risk.assertions || "—"}
                </div>
                <div>
                  <span className="font-medium text-foreground">Response:</span>{" "}
                  {risk.plannedResponse || "—"} ({risk.linkedProcedures}{" "}
                  linked procedures)
                </div>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={AlertTriangle}
          title="No risks recorded"
          body="Record the risks of material misstatement this engagement must respond to."
          action={addDialog}
        />
      )}
    </div>
  );
}

export function ProceduresTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [area, setArea] = useState("");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [conclusionFor, setConclusionFor] = useState<string | null>(null);
  const [conclusion, setConclusion] = useState("");

  const procedures = useApiQuery<ProcedureRow[]>(
    `/api/audit/engagements/${engagementId}/procedures`,
    { queryKey: ["audit-procedures", engagementId] },
  );

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Saved.");
      setOpen(false);
      setConclusionFor(null);
      setConclusion("");
      setTitle("");
      queryClient.invalidateQueries({
        queryKey: ["audit-procedures", engagementId],
      });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const addDialog = (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="font-semibold">
          <Plus className="mr-2 h-4 w-4" />
          Add procedure
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Add an audit procedure</DialogTitle>
        </DialogHeader>
        <div className="space-y-3 py-2">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="space-y-1.5">
              <Label>Area</Label>
              <Input
                value={area}
                onChange={(e) => setArea(e.target.value)}
                placeholder="Revenue"
              />
            </div>
            <div className="space-y-1.5">
              <Label>Title</Label>
              <Input
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="Cut-off testing"
              />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>Description</Label>
            <Textarea
              rows={3}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </div>
        </div>
        <DialogFooter>
          <Button
            disabled={action.isPending || !title.trim()}
            onClick={() =>
              action.mutate({
                url: `/api/audit/engagements/${engagementId}/procedures`,
                body: { area, title: title.trim(), description, riskIds: [] },
              })
            }
          >
            Add procedure
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );

  if (procedures.isLoading) return <LoadingRows />;
  if (procedures.isError)
    return (
      <ErrorCard
        errors={procedures.error}
        onRetry={() => procedures.refetch()}
      />
    );

  const update = (procedureId: string, body: object) =>
    action.mutate({
      url: `/api/audit/engagements/${engagementId}/procedures/${procedureId}`,
      body,
    });

  return (
    <div className="space-y-4">
      <div className="flex justify-end">{addDialog}</div>
      {procedures.data && procedures.data.length > 0 ? (
        <div className="space-y-3">
          {procedures.data.map((procedure) => (
            <div
              key={procedure.id}
              className="rounded-xl border border-border/60 bg-card p-4"
            >
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div>
                  <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                    {procedure.area || "General"}
                  </span>
                  <div className="font-semibold">{procedure.title}</div>
                </div>
                <div className="flex items-center gap-2">
                  <StatusPill value={procedure.status} />
                  {procedure.status === "Planned" && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => update(procedure.id, { action: "Start" })}
                    >
                      Start
                    </Button>
                  )}
                  {procedure.status === "InProgress" && (
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => setConclusionFor(procedure.id)}
                    >
                      Complete…
                    </Button>
                  )}
                </div>
              </div>
              {procedure.description && (
                <p className="mt-1.5 text-sm text-muted-foreground">
                  {procedure.description}
                </p>
              )}
              {procedure.conclusion && (
                <p className="mt-2 rounded-lg bg-muted/40 p-2 text-sm">
                  <span className="font-medium">Conclusion:</span>{" "}
                  {procedure.conclusion}
                </p>
              )}
              {conclusionFor === procedure.id && (
                <div className="mt-3 flex gap-2">
                  <Input
                    value={conclusion}
                    onChange={(e) => setConclusion(e.target.value)}
                    placeholder="Conclusion reached"
                  />
                  <Button
                    size="sm"
                    disabled={!conclusion.trim() || action.isPending}
                    onClick={() =>
                      update(procedure.id, {
                        action: "Complete",
                        conclusion: conclusion.trim(),
                      })
                    }
                  >
                    Complete
                  </Button>
                </div>
              )}
            </div>
          ))}
        </div>
      ) : (
        <EmptyCard
          icon={ListChecks}
          title="No procedures yet"
          body="Plan the audit procedures that respond to the identified risks."
          action={addDialog}
        />
      )}
    </div>
  );
}

interface LinkedContext {
  isAvailable: boolean;
  unavailableReason: string | null;
  entities: {
    id: string;
    name: string;
    baseCurrency: string;
    periods: { id: string; name: string; status: string }[];
  }[];
}

export function TrialBalanceTab({ engagementId }: { engagementId: string }) {
  const queryClient = useQueryClient();
  const [periodLabel, setPeriodLabel] = useState("");
  const [csvContent, setCsvContent] = useState("");
  const [linkedEntity, setLinkedEntity] = useState("");
  const [linkedPeriod, setLinkedPeriod] = useState("");

  const trialBalance = useApiQuery<TrialBalanceView>(
    `/api/audit/engagements/${engagementId}/trial-balance`,
    { queryKey: ["audit-tb", engagementId], retry: false },
  );

  const linked = useApiQuery<LinkedContext>("/api/audit/accounting-context", {
    queryKey: ["audit-linked-context"],
  });

  const action = useApiAction({
    onSuccess: () => {
      toast.success("Trial balance imported.");
      setCsvContent("");
      queryClient.invalidateQueries({ queryKey: ["audit-tb", engagementId] });
    },
    onError: (errors) => toast.error(errors.join(" ")),
  });

  const entityPeriods =
    linked.data?.entities.find((entity) => entity.id === linkedEntity)
      ?.periods ?? [];

  return (
    <div className="space-y-6">
      <div className="grid gap-6 lg:grid-cols-2">
        <Card className="border-border/60">
          <CardHeader className="pb-3">
            <CardTitle className="font-display text-base font-semibold">
              Import from CSV
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="space-y-1.5">
              <Label>Period label</Label>
              <Input
                value={periodLabel}
                onChange={(e) => setPeriodLabel(e.target.value)}
                placeholder="FY2026"
              />
            </div>
            <div className="space-y-1.5">
              <Label>CSV content (code, name, debit, credit)</Label>
              <Textarea
                rows={5}
                className="font-mono text-xs"
                value={csvContent}
                onChange={(e) => setCsvContent(e.target.value)}
                placeholder={"1000,Cash,50000,0\n3000,Equity,0,50000"}
              />
              <Input
                type="file"
                accept=".csv,text/csv"
                onChange={async (e) => {
                  const file = e.target.files?.[0];
                  if (file) setCsvContent(await file.text());
                }}
              />
            </div>
            <Button
              className="font-semibold"
              disabled={!periodLabel || !csvContent || action.isPending}
              onClick={() =>
                action.mutate({
                  url: `/api/audit/engagements/${engagementId}/trial-balance`,
                  body: { periodLabel, csvContent },
                })
              }
            >
              <Upload className="mr-2 h-4 w-4" />
              Import CSV
            </Button>
          </CardContent>
        </Card>

        <Card className="border-border/60">
          <CardHeader className="pb-3">
            <CardTitle className="font-display text-base font-semibold">
              Import from Ledgance Accounting
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            {linked.data?.isAvailable ? (
              <>
                <div className="space-y-1.5">
                  <Label>Accounting entity</Label>
                  <FieldSelect
                    value={linkedEntity}
                    onChange={(e) => {
                      setLinkedEntity(e.target.value);
                      setLinkedPeriod("");
                    }}
                  >
                    <option value="">Select…</option>
                    {linked.data.entities.map((entity) => (
                      <option key={entity.id} value={entity.id}>
                        {entity.name} ({entity.baseCurrency})
                      </option>
                    ))}
                  </FieldSelect>
                </div>
                <div className="space-y-1.5">
                  <Label>Fiscal period</Label>
                  <FieldSelect
                    value={linkedPeriod}
                    onChange={(e) => setLinkedPeriod(e.target.value)}
                    disabled={!linkedEntity}
                  >
                    <option value="">Select…</option>
                    {entityPeriods.map((period) => (
                      <option key={period.id} value={period.id}>
                        {period.name} ({period.status})
                      </option>
                    ))}
                  </FieldSelect>
                </div>
                <Button
                  className="font-semibold"
                  disabled={!linkedEntity || !linkedPeriod || action.isPending}
                  onClick={() =>
                    action.mutate({
                      url: `/api/audit/engagements/${engagementId}/trial-balance/from-accounting`,
                      body: {
                        accountingEntityId: linkedEntity,
                        accountingPeriodId: linkedPeriod,
                      },
                    })
                  }
                >
                  Import from books
                </Button>
              </>
            ) : (
              <p className="text-sm text-muted-foreground">
                {linked.data?.unavailableReason ??
                  "Checking whether the organization's Ledgance Accounting books are linked…"}{" "}
                CSV import always works.
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {trialBalance.data ? (
        <div className="overflow-x-auto rounded-2xl border border-border/60 bg-card">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-border/60 px-4 py-3">
            <div className="text-sm font-semibold">
              {trialBalance.data.periodLabel} · source{" "}
              {trialBalance.data.source.replace(/([a-z])([A-Z])/g, "$1 $2")} ·
              imported {fmtDate(trialBalance.data.importedAt)}
            </div>
            <StatusPill
              value={trialBalance.data.isBalanced ? "Completed" : "Critical"}
            />
          </div>
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Code</TableHead>
                <TableHead>Account</TableHead>
                <TableHead className="text-right">Debit</TableHead>
                <TableHead className="text-right">Credit</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {trialBalance.data.lines.map((line, index) => (
                <TableRow key={`${line.accountCode}-${index}`}>
                  <TableCell className="font-mono text-xs">
                    {line.accountCode}
                  </TableCell>
                  <TableCell>{line.accountName}</TableCell>
                  <TableCell className="text-right font-mono text-xs">
                    {line.debit ? fmtMoney(line.debit) : ""}
                  </TableCell>
                  <TableCell className="text-right font-mono text-xs">
                    {line.credit ? fmtMoney(line.credit) : ""}
                  </TableCell>
                </TableRow>
              ))}
              <TableRow className="font-semibold">
                <TableCell colSpan={2}>Totals</TableCell>
                <TableCell className="text-right font-mono text-xs">
                  {fmtMoney(trialBalance.data.totalDebits)}
                </TableCell>
                <TableCell className="text-right font-mono text-xs">
                  {fmtMoney(trialBalance.data.totalCredits)}
                </TableCell>
              </TableRow>
            </TableBody>
          </Table>
        </div>
      ) : (
        <EmptyCard
          icon={FileSpreadsheet}
          title="No trial balance imported"
          body="Import the client's trial balance from CSV — or straight from the organization's Ledgance Accounting books when sharing is enabled."
        />
      )}
    </div>
  );
}
