/** Shapes mirroring the Audit API's DTOs — keep in sync with the backend slices. */

export interface ClientRow {
  id: string;
  name: string;
  email: string;
  phone: string;
  industry: string;
  contactName: string;
  isArchived: boolean;
  createdAt: string;
}

export interface EngagementListRow {
  id: string;
  clientId: string;
  clientName: string;
  name: string;
  type: string;
  status: string;
  periodStart: string;
  periodEnd: string;
  budgetHours: number;
  createdAt: string;
}

export interface PlanView {
  scope: string;
  objectives: string;
  strategy: string;
  timelineStart: string | null;
  timelineEnd: string | null;
  isApproved: boolean;
}

export interface MaterialityView {
  overallAmount: number;
  performanceAmount: number;
  clearlyTrivialThreshold: number;
  basis: string;
  rationale: string;
}

export interface TeamMemberView {
  memberId: string;
  userId: string;
  displayName: string;
  email: string;
  role: string;
}

export interface EngagementDetail extends EngagementListRow {
  fiscalYearEnd: string | null;
  plan: PlanView | null;
  materiality: MaterialityView | null;
  team: TeamMemberView[];
}

export interface RiskRow {
  id: string;
  title: string;
  description: string;
  assertions: string;
  likelihood: string;
  impact: string;
  level: string;
  plannedResponse: string;
  linkedProcedures: number;
}

export interface ProcedureRow {
  id: string;
  area: string;
  title: string;
  description: string;
  status: string;
  riskIds: string[];
  assigneeUserId: string | null;
  conclusion: string | null;
  completedAt: string | null;
}

export interface WorkingPaperRow {
  id: string;
  reference: string;
  title: string;
  status: string;
  preparedBy: string | null;
  reviewedBy: string | null;
  approvedBy: string | null;
  openNotes: number;
}

export interface FindingRow {
  id: string;
  title: string;
  description: string;
  severity: string;
  status: string;
  recommendation: string;
  resolution: string | null;
  evidenceIds: string[];
  raisedBy: string;
  raisedAt: string;
}

export interface EvidenceRow {
  id: string;
  workingPaperId: string | null;
  procedureId: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  version: number;
  description: string;
  uploadedBy: string;
  uploadedAt: string;
}

export interface TrialBalanceLineView {
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
}

export interface TrialBalanceView {
  importId: string;
  source: string;
  periodLabel: string;
  totalDebits: number;
  totalCredits: number;
  isBalanced: boolean;
  importedAt: string;
  lines: TrialBalanceLineView[];
}

export interface AuditReportView {
  id: string;
  opinion: string;
  basisForOpinion: string;
  keyAuditMatters: string;
  otherInformation: string;
  isFinalized: boolean;
  finalizedBy: string | null;
  finalizedAt: string | null;
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

export interface OrganizationMemberRow {
  userId: string;
  displayName: string;
  email: string;
  role: string;
}

export const engagementTypes = [
  "FinancialStatement",
  "Internal",
  "Compliance",
  "Tax",
  "LimitedReview",
  "Compilation",
] as const;

export const engagementStatuses = [
  "Planning",
  "Fieldwork",
  "Review",
  "SignedOff",
  "Completed",
] as const;

export const riskRatings = ["Low", "Medium", "High"] as const;
export const findingSeverities = ["Low", "Medium", "High", "Critical"] as const;
export const engagementRoles = ["Staff", "Senior", "Manager", "Partner"] as const;
export const auditOpinions = [
  "Unqualified",
  "Qualified",
  "Adverse",
  "Disclaimer",
] as const;
