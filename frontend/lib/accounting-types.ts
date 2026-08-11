/** Shapes mirroring the Accounting API's DTOs — keep in sync with the backend slices. */

export interface EntityRow {
  id: string;
  name: string;
  legalName: string;
  baseCurrency: string;
  isArchived: boolean;
  createdAt: string;
}

/** Row of `GET /api/accounting/entities/paged` — the entity card grid's shape. */
export interface EntityCardRow extends EntityRow {
  openPeriods: number;
  totalPeriods: number;
}

export interface AccountRow {
  id: string;
  code: string;
  name: string;
  type: string;
  normalBalance: string;
  classification: string;
  parentAccountId: string | null;
  hasChildren: boolean;
  isActive: boolean;
}

export interface FiscalPeriodRow {
  id: string;
  name: string;
  startDate: string;
  endDate: string;
  status: string;
  closedBy: string | null;
  closedAt: string | null;
}

export interface JournalEntryRow {
  id: string;
  entryNumber: number;
  entryDate: string;
  memo: string;
  reference: string;
  status: string;
  totalDebits: number;
  totalCredits: number;
  reversalOfEntryId: string | null;
  reversedByEntryId: string | null;
  postedAt: string | null;
}

export interface JournalLineInput {
  accountId: string;
  description: string;
  debit: number;
  credit: number;
}

export interface TrialBalanceRowView {
  accountId: string;
  accountCode: string;
  accountName: string;
  type: string;
  totalDebits: number;
  totalCredits: number;
  debitBalance: number;
  creditBalance: number;
}

export interface TrialBalanceView {
  asOf: string;
  rows: TrialBalanceRowView[];
  totalDebitBalances: number;
  totalCreditBalances: number;
  isBalanced: boolean;
}

export interface ReportLineView {
  accountId: string;
  accountCode: string;
  accountName: string;
  amount: number;
}

export interface IncomeStatementView {
  periodName: string;
  from: string;
  to: string;
  revenue: ReportLineView[];
  expenses: ReportLineView[];
  totalRevenue: number;
  totalExpenses: number;
  netIncome: number;
}

export interface BalanceSheetView {
  asOf: string;
  assets: ReportLineView[];
  liabilities: ReportLineView[];
  equity: ReportLineView[];
  totalAssets: number;
  totalLiabilities: number;
  totalEquity: number;
  currentEarnings: number;
  isBalanced: boolean;
}

export interface ReconciliationRow {
  id: string;
  accountId: string;
  accountCode: string;
  accountName: string;
  statementDate: string;
  statementBalance: number;
  status: string;
  clearedBalance: number | null;
  difference: number | null;
  explanation: string | null;
  startedAt: string;
  completedAt: string | null;
}

export interface DocumentRow {
  id: string;
  journalEntryId: string | null;
  reconciliationId: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  description: string;
  uploadedBy: string;
  uploadedAt: string;
}

export interface ActivityRow {
  id: string;
  action: string;
  subjectType: string;
  subjectId: string;
  summary: string;
  actorUserId: string;
  actorEmail: string;
  occurredAt: string;
}

export const accountTypes = [
  "Asset",
  "Liability",
  "Equity",
  "Revenue",
  "Expense",
] as const;
