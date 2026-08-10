namespace Ledgance.Accounting.Ledger.Domain {
    /// <summary>
    /// A source document supporting the books — an invoice, receipt, statement or similar.
    /// Documents may be linked to the journal entry or reconciliation they support.
    /// </summary>
    public sealed class AccountingDocument {
        private AccountingDocument() { }

        public Guid Id { get; private set; }
        public Guid EntityId { get; private set; }
        public Guid? JournalEntryId { get; private set; }
        public Guid? ReconciliationId { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;
        public long SizeBytes { get; private set; }
        public string StoragePath { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public Guid UploadedBy { get; private set; }
        public DateTime UploadedAt { get; private set; }

        public static AccountingDocument Upload(Guid entityId, Guid? journalEntryId,
            Guid? reconciliationId, string fileName, string contentType, long sizeBytes,
            string storagePath, string description, Guid uploadedBy) {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
            ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);

            return new AccountingDocument {
                Id = Guid.NewGuid(),
                EntityId = entityId,
                JournalEntryId = journalEntryId,
                ReconciliationId = reconciliationId,
                FileName = fileName.Trim(),
                ContentType = contentType.Trim(),
                SizeBytes = sizeBytes,
                StoragePath = storagePath,
                Description = description.Trim(),
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow
            };
        }

        public static AccountingDocument Restore(Guid id, Guid entityId, Guid? journalEntryId,
            Guid? reconciliationId, string fileName, string contentType, long sizeBytes,
            string storagePath, string description, Guid uploadedBy, DateTime uploadedAt) =>
            new() {
                Id = id,
                EntityId = entityId,
                JournalEntryId = journalEntryId,
                ReconciliationId = reconciliationId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                StoragePath = storagePath,
                Description = description,
                UploadedBy = uploadedBy,
                UploadedAt = uploadedAt
            };
    }
}
