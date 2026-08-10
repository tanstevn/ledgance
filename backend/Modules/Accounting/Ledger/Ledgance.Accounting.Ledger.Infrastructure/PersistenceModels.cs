using Ledgance.Shared.Infrastructure.Supabase;
using Newtonsoft.Json;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Ledgance.Accounting.Ledger.Infrastructure {
    [Table("accounting_entities")]
    public class AccountingEntityModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("legal_name")]
        public string LegalName { get; set; } = string.Empty;

        [Column("base_currency")]
        public string BaseCurrency { get; set; } = string.Empty;

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("accounting_accounts")]
    public class AccountModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("entity_id")]
        public Guid EntityId { get; set; }

        [Column("code")]
        public string Code { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("type")]
        public string Type { get; set; } = string.Empty;

        [Column("classification")]
        public string Classification { get; set; } = string.Empty;

        [Column("parent_account_id")]
        public Guid? ParentAccountId { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("accounting_fiscal_periods")]
    public class FiscalPeriodModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("entity_id")]
        public Guid EntityId { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("start_date")]
        public DateTime StartDate { get; set; }

        [Column("end_date")]
        public DateTime EndDate { get; set; }

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("closed_by")]
        public Guid? ClosedBy { get; set; }

        [Column("closed_at")]
        public DateTime? ClosedAt { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("accounting_journal_entries")]
    public class JournalEntryModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("entity_id")]
        public Guid EntityId { get; set; }

        [Column("entry_number")]
        public long EntryNumber { get; set; }

        [Column("entry_date")]
        public DateTime EntryDate { get; set; }

        [Column("memo")]
        public string Memo { get; set; } = string.Empty;

        [Column("reference")]
        public string Reference { get; set; } = string.Empty;

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("lines")]
        public List<JournalLineDoc> Lines { get; set; } = [];

        [Column("reversal_of_entry_id")]
        public Guid? ReversalOfEntryId { get; set; }

        [Column("reversed_by_entry_id")]
        public Guid? ReversedByEntryId { get; set; }

        [Column("created_by")]
        public Guid CreatedBy { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("posted_by")]
        public Guid? PostedBy { get; set; }

        [Column("posted_at")]
        public DateTime? PostedAt { get; set; }
    }

    public class JournalLineDoc {
        [JsonProperty("accountId")] public Guid AccountId { get; set; }
        [JsonProperty("description")] public string Description { get; set; } = string.Empty;
        [JsonProperty("debit")] public decimal Debit { get; set; }
        [JsonProperty("credit")] public decimal Credit { get; set; }
    }

    [Table("accounting_ledger_lines")]
    public class LedgerLineModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("entity_id")]
        public Guid EntityId { get; set; }

        [Column("entry_id")]
        public Guid EntryId { get; set; }

        [Column("entry_number")]
        public long EntryNumber { get; set; }

        [Column("entry_date")]
        public DateTime EntryDate { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("debit")]
        public decimal Debit { get; set; }

        [Column("credit")]
        public decimal Credit { get; set; }
    }

    [Table("accounting_reconciliations")]
    public class ReconciliationModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("entity_id")]
        public Guid EntityId { get; set; }

        [Column("account_id")]
        public Guid AccountId { get; set; }

        [Column("statement_date")]
        public DateTime StatementDate { get; set; }

        [Column("statement_balance")]
        public decimal StatementBalance { get; set; }

        [Column("status")]
        public string Status { get; set; } = string.Empty;

        [Column("cleared_line_ids")]
        public List<Guid> ClearedLineIds { get; set; } = [];

        [Column("cleared_balance")]
        public decimal? ClearedBalance { get; set; }

        [Column("difference")]
        public decimal? Difference { get; set; }

        [Column("explanation")]
        public string? Explanation { get; set; }

        [Column("started_by")]
        public Guid StartedBy { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; }

        [Column("completed_by")]
        public Guid? CompletedBy { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }
    }

    [Table("accounting_documents")]
    public class AccountingDocumentModel : BaseModel, IEntityModel, IOrganizationOwned {
        [PrimaryKey("id", true)]
        public Guid Id { get; set; }

        [Column("organization_id")]
        public Guid OrganizationId { get; set; }

        [Column("entity_id")]
        public Guid EntityId { get; set; }

        [Column("journal_entry_id")]
        public Guid? JournalEntryId { get; set; }

        [Column("reconciliation_id")]
        public Guid? ReconciliationId { get; set; }

        [Column("file_name")]
        public string FileName { get; set; } = string.Empty;

        [Column("content_type")]
        public string ContentType { get; set; } = string.Empty;

        [Column("size_bytes")]
        public long SizeBytes { get; set; }

        [Column("storage_path")]
        public string StoragePath { get; set; } = string.Empty;

        [Column("description")]
        public string Description { get; set; } = string.Empty;

        [Column("uploaded_by")]
        public Guid UploadedBy { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; }
    }
}
