export type EngagementStatus =
  | "planning"
  | "fieldwork"
  | "review"
  | "sign-off"
  | "completed";

export type EngagementType =
  | "financial-statement"
  | "internal"
  | "compliance"
  | "tax"
  | "review"
  | "compilation";

export type SignOffStatus = "pending" | "prepared" | "reviewed" | "approved";

export type DocumentCategory =
  | "evidence"
  | "financial"
  | "correspondence"
  | "report"
  | "supporting";

export type AccountMappingStatus = "mapped" | "unmapped" | "ignored";

export interface Organization {
  id: string;
  name: string;
  logoColor: string;
  plan: "starter" | "professional" | "enterprise";
}

export interface User {
  id: string;
  name: string;
  email: string;
  role: "partner" | "manager" | "senior" | "staff";
  initials: string;
}

export interface Client {
  id: string;
  name: string;
  industry: string;
  contactName: string;
  contactEmail: string;
  contactPhone: string;
  website: string;
  address: string;
  createdAt: string;
  logoColor: string;
}

export interface Engagement {
  id: string;
  clientId: string;
  name: string;
  type: EngagementType;
  status: EngagementStatus;
  partner: string;
  manager: string;
  fiscalYearEnd: string;
  budgetHours: number;
  actualHours: number;
  progress: number;
  startDate: string;
  endDate: string;
  team: string[];
}

export interface DocumentVersion {
  version: number;
  uploadedBy: string;
  uploadedAt: string;
  size: string;
  note: string;
}

export interface Document {
  id: string;
  engagementId: string;
  name: string;
  category: DocumentCategory;
  type: string;
  status: "current" | "superseded" | "archived";
  uploadedBy: string;
  uploadedAt: string;
  size: string;
  versions: DocumentVersion[];
  tags: string[];
}

export interface ReviewNote {
  id: string;
  author: string;
  authorInitials: string;
  createdAt: string;
  body: string;
  status: "open" | "resolved" | "cleared";
  reply?: string;
  replyAuthor?: string;
  replyAt?: string;
}

export interface CrossReference {
  id: string;
  fromPaper: string;
  toPaper: string;
  label: string;
  type: "supports" | "references" | "reconciles";
}

export interface WorkingPaper {
  id: string;
  engagementId: string;
  reference: string;
  title: string;
  area: string;
  description: string;
  preparedBy: string;
  reviewedBy: string | null;
  approvedBy: string | null;
  signOffStatus: SignOffStatus;
  preparedAt: string | null;
  reviewedAt: string | null;
  approvedAt: string | null;
  reviewNotes: ReviewNote[];
  crossReferences: CrossReference[];
  conclusion: string;
  riskLevel: "low" | "medium" | "high";
}

export interface TrialBalanceEntry {
  id: string;
  accountNumber: string;
  accountName: string;
  debit: number;
  credit: number;
  mapping: string | null;
  mappingStatus: AccountMappingStatus;
  assertion: string | null;
  risk: "low" | "medium" | "high";
}

export interface ActivityItem {
  id: string;
  actor: string;
  actorInitials: string;
  action: string;
  target: string;
  timestamp: string;
  type: "sign-off" | "document" | "note" | "engagement" | "mapping";
}
