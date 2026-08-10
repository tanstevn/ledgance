using Ledgance.Shared.Infrastructure.Supabase;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Ledgance.Audit.Engagement.Infrastructure {
    [Table("audit_engagements")]
    public class EngagementModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("client_id")]
        public Guid ClientId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("period_start")]
        public DateTime PeriodStart { get; set; }

        [Column("period_end")]
        public DateTime PeriodEnd { get; set; }

        [Column("fiscal_year_end")]
        public DateTime? FiscalYearEnd { get; set; }

        [Column("budget_hours")]
        public decimal BudgetHours { get; set; }

        [Column("created_by")]
        public Guid CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("plan")]
        public PlanDoc? Plan { get; set; }

        [Column("materiality")]
        public MaterialityDoc? Materiality { get; set; }
    }

    public class PlanDoc {
        [JsonProperty("scope")] public string Scope { get; set; } = string.Empty;
        [JsonProperty("objectives")] public string Objectives { get; set; } = string.Empty;
        [JsonProperty("strategy")] public string Strategy { get; set; } = string.Empty;
        [JsonProperty("timelineStart")] public DateTime? TimelineStart { get; set; }
        [JsonProperty("timelineEnd")] public DateTime? TimelineEnd { get; set; }
        [JsonProperty("isApproved")] public bool IsApproved { get; set; }
        [JsonProperty("approvedBy")] public Guid? ApprovedBy { get; set; }
        [JsonProperty("approvedAt")] public DateTime? ApprovedAt { get; set; }
    }

    public class MaterialityDoc {
        [JsonProperty("overallAmount")] public decimal OverallAmount { get; set; }
        [JsonProperty("performanceAmount")] public decimal PerformanceAmount { get; set; }
        [JsonProperty("clearlyTrivialThreshold")] public decimal ClearlyTrivialThreshold { get; set; }
        [JsonProperty("basis")] public string Basis { get; set; } = string.Empty;
        [JsonProperty("rationale")] public string Rationale { get; set; } = string.Empty;
    }

    [Table("audit_engagement_members")]
    public class TeamMemberModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("role")]
        public string Role { get; set; } = string.Empty;

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; }
    }

    [Table("audit_risks")]
    public class RiskModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("assertions")]
        public string Assertions { get; set; } = string.Empty;

        [Column("likelihood")]
        public int Likelihood { get; set; }

        [Column("impact")]
        public int Impact { get; set; }

        [Column("planned_response")]
        public string PlannedResponse { get; set; } = string.Empty;
    }

    [Table("audit_procedures")]
    public class ProcedureModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("area")]
        public string Area { get; set; } = string.Empty;

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("risk_ids")]
        public List<Guid> RiskIds { get; set; } = [];

        [Column("assignee_user_id")]
        public Guid? AssigneeUserId { get; set; }

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("conclusion")]
        public string? Conclusion { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }

    [Table("audit_working_papers")]
    public class WorkingPaperModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("reference")]
        public string Reference { get; set; } = string.Empty;

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("content")]
        public string Content { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("prepared_by")]
        public Guid? PreparedBy { get; set; }

        [Column("prepared_at")]
        public DateTime? PreparedAt { get; set; }

        [Column("reviewed_by")]
        public Guid? ReviewedBy { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }

        [Column("approved_by")]
        public Guid? ApprovedBy { get; set; }

        [Column("approved_at")]
        public DateTime? ApprovedAt { get; set; }

        [Column("notes")]
        public List<ReviewNoteDoc> Notes { get; set; } = [];
    }

    public class ReviewNoteDoc {
        [JsonProperty("id")] public Guid Id { get; set; }
        [JsonProperty("authorUserId")] public Guid AuthorUserId { get; set; }
        [JsonProperty("text")] public string Text { get; set; } = string.Empty;
        [JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
        [JsonProperty("resolvedBy")] public Guid? ResolvedBy { get; set; }
        [JsonProperty("resolvedAt")] public DateTime? ResolvedAt { get; set; }
        [JsonProperty("resolution")] public string? Resolution { get; set; }
    }

    [Table("audit_evidence")]
    public class EvidenceModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("working_paper_id")]
        public Guid? WorkingPaperId { get; set; }

        [Column("procedure_id")]
        public Guid? ProcedureId { get; set; }

        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Column("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("storage_path")]
        public string StoragePath { get; set; } = string.Empty;

        [Column("version")]
        public int Version { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("uploaded_by")]
        public Guid UploadedBy { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; }
    }

    [Table("audit_findings")]
    public class FindingModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("severity")]
        public string Severity { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("recommendation")]
        public string Recommendation { get; set; } = string.Empty;

        [Column("resolution")]
        public string? Resolution { get; set; }

        [Column("evidence_ids")]
        public List<Guid> EvidenceIds { get; set; } = [];

        [Column("raised_by")]
        public Guid RaisedBy { get; set; }

        [Column("raised_at")]
        public DateTime RaisedAt { get; set; }
    }

    [Table("audit_reports")]
    public class ReportModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("opinion")]
        public string Opinion { get; set; } = string.Empty;

        [Column("basis_for_opinion")]
        public string BasisForOpinion { get; set; } = string.Empty;

        [Column("key_audit_matters")]
        public string KeyAuditMatters { get; set; } = string.Empty;

        [Column("other_information")]
        public string OtherInformation { get; set; } = string.Empty;

        [Column("is_finalized")]
        public bool IsFinalized { get; set; }

        [Column("finalized_by")]
        public Guid? FinalizedBy { get; set; }

        [Column("finalized_at")]
        public DateTime? FinalizedAt { get; set; }

        [Column("updated_at")]
        public DateTime UpdatedAt { get; set; }
    }

    [Table("audit_trial_balances")]
    public class TrialBalanceModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("engagement_id")]
        public Guid EngagementId { get; set; }

        [Column("source")]
        public string Source { get; set; } = string.Empty;

        [Column("period_label")]
        public string PeriodLabel { get; set; } = string.Empty;

        [Column("lines")]
        public List<TrialBalanceLineDoc> Lines { get; set; } = [];

        [Column("total_debits")]
        public decimal TotalDebits { get; set; }

        [Column("total_credits")]
        public decimal TotalCredits { get; set; }

        [Column("imported_by")]
        public Guid ImportedBy { get; set; }

        [Column("imported_at")]
        public DateTime ImportedAt { get; set; }
    }

    public class TrialBalanceLineDoc {
        [JsonProperty("accountCode")] public string AccountCode { get; set; } = string.Empty;
        [JsonProperty("accountName")] public string AccountName { get; set; } = string.Empty;
        [JsonProperty("debit")] public decimal Debit { get; set; }
        [JsonProperty("credit")] public decimal Credit { get; set; }
    }
}
