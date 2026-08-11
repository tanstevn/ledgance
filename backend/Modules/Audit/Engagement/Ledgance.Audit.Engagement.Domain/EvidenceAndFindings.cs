using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Engagement.Domain {
    public enum EvidenceCategory { Evidence, Financial, Correspondence, Supporting }

    /// <summary>
    /// One retained version of an evidence file. Prior versions stay addressable — an auditor
    /// must be able to show what a paper referenced at the time, not only the latest file.
    /// </summary>
    public sealed record EvidenceVersion(
        int Version,
        string StoragePath,
        long SizeBytes,
        string ContentType,
        string Note,
        Guid UploadedBy,
        DateTime UploadedAt);

    public sealed class Evidence {
        private readonly List<EvidenceVersion> _priorVersions = [];
        private readonly List<string> _tags = [];

        private Evidence() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public Guid? WorkingPaperId { get; private set; }
        public Guid? ProcedureId { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public string ContentType { get; private set; } = string.Empty;
        public long SizeBytes { get; private set; }
        public string StoragePath { get; private set; } = string.Empty;
        public int Version { get; private set; }
        public string Description { get; private set; } = string.Empty;
        public EvidenceCategory Category { get; private set; }
        public IReadOnlyList<string> Tags => _tags;
        public Guid UploadedBy { get; private set; }
        public DateTime UploadedAt { get; private set; }

        /// <summary>Superseded versions, oldest first. The current version lives on the entity.</summary>
        public IReadOnlyList<EvidenceVersion> PriorVersions => _priorVersions;

        /// <summary>Every version newest first — the shape a version history renders.</summary>
        public IReadOnlyList<EvidenceVersion> AllVersions() =>
            [.. _priorVersions
                .Append(new EvidenceVersion(Version, StoragePath, SizeBytes, ContentType,
                    Description, UploadedBy, UploadedAt))
                .OrderByDescending(version => version.Version)];

        public static Evidence Upload(Guid engagementId, Guid? workingPaperId,
            Guid? procedureId, string fileName, string contentType, long sizeBytes,
            string storagePath, string description, EvidenceCategory category,
            IEnumerable<string> tags, Guid uploadedBy) {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

            if (sizeBytes <= 0) {
                throw new DomainRuleException("Evidence must contain file content.");
            }

            var evidence = new Evidence {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                WorkingPaperId = workingPaperId,
                ProcedureId = procedureId,
                FileName = fileName.Trim(),
                ContentType = contentType,
                SizeBytes = sizeBytes,
                StoragePath = storagePath,
                Version = 1,
                Description = description.Trim(),
                Category = category,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow
            };

            evidence.SetTags(tags);
            return evidence;
        }

        public static Evidence Restore(Guid id, Guid engagementId, Guid? workingPaperId,
            Guid? procedureId, string fileName, string contentType, long sizeBytes,
            string storagePath, int version, string description, EvidenceCategory category,
            IEnumerable<string> tags, IEnumerable<EvidenceVersion> priorVersions,
            Guid uploadedBy, DateTime uploadedAt) {
            var evidence = new Evidence {
                Id = id,
                EngagementId = engagementId,
                WorkingPaperId = workingPaperId,
                ProcedureId = procedureId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                StoragePath = storagePath,
                Version = version,
                Description = description,
                Category = category,
                UploadedBy = uploadedBy,
                UploadedAt = uploadedAt
            };

            evidence._tags.AddRange(tags);
            evidence._priorVersions.AddRange(priorVersions);
            return evidence;
        }

        /// <summary>
        /// Superseding keeps the same evidence identity while pointing at new file content, so
        /// references from papers and findings stay valid across versions. The outgoing
        /// version is retained, never overwritten — the trail must show what each version said.
        /// </summary>
        public void Supersede(string storagePath, long sizeBytes, string contentType,
            string note, Guid uploadedBy) {
            if (sizeBytes <= 0) {
                throw new DomainRuleException("Evidence must contain file content.");
            }

            _priorVersions.Add(new EvidenceVersion(Version, StoragePath, SizeBytes,
                ContentType, Description, UploadedBy, UploadedAt));

            StoragePath = storagePath;
            SizeBytes = sizeBytes;
            ContentType = contentType;
            Version++;
            Description = note.Trim();
            UploadedBy = uploadedBy;
            UploadedAt = DateTime.UtcNow;
        }

        public EvidenceVersion? FindVersion(int version) =>
            AllVersions().FirstOrDefault(entry => entry.Version == version);

        private void SetTags(IEnumerable<string> tags) =>
            _tags.AddRange(tags
                .Select(tag => tag.Trim().ToLowerInvariant())
                .Where(tag => tag.Length > 0)
                .Distinct()
                .Take(10));
    }

    public enum FindingSeverity { Low, Medium, High, Critical }

    public enum FindingStatus { Open, Resolved, RiskAccepted, Closed }

    public sealed class Finding {
        private Finding() { }

        public Guid Id { get; private set; }
        public Guid EngagementId { get; private set; }
        public string Title { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;
        public FindingSeverity Severity { get; private set; }
        public FindingStatus Status { get; private set; }
        public string Recommendation { get; private set; } = string.Empty;
        public string? Resolution { get; private set; }
        public List<Guid> EvidenceIds { get; private set; } = [];
        public Guid RaisedBy { get; private set; }
        public DateTime RaisedAt { get; private set; }

        public bool IsOpen => Status == FindingStatus.Open;

        public static Finding Raise(Guid engagementId, string title, string description,
            FindingSeverity severity, string recommendation, IEnumerable<Guid> evidenceIds,
            Guid raisedBy) {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            ArgumentException.ThrowIfNullOrWhiteSpace(description);

            return new Finding {
                Id = Guid.NewGuid(),
                EngagementId = engagementId,
                Title = title.Trim(),
                Description = description.Trim(),
                Severity = severity,
                Status = FindingStatus.Open,
                Recommendation = recommendation.Trim(),
                EvidenceIds = evidenceIds.Distinct().ToList(),
                RaisedBy = raisedBy,
                RaisedAt = DateTime.UtcNow
            };
        }

        public static Finding Restore(Guid id, Guid engagementId, string title,
            string description, FindingSeverity severity, FindingStatus status,
            string recommendation, string? resolution, List<Guid> evidenceIds,
            Guid raisedBy, DateTime raisedAt) =>
            new() {
                Id = id,
                EngagementId = engagementId,
                Title = title,
                Description = description,
                Severity = severity,
                Status = status,
                Recommendation = recommendation,
                Resolution = resolution,
                EvidenceIds = evidenceIds,
                RaisedBy = raisedBy,
                RaisedAt = raisedAt
            };

        public void Resolve(string resolution) {
            EnsureStatus(FindingStatus.Open, "Only an open finding can be resolved.");
            RequireText(resolution, "Resolving a finding requires a resolution note.");

            Status = FindingStatus.Resolved;
            Resolution = resolution.Trim();
        }

        public void AcceptRisk(string justification) {
            EnsureStatus(FindingStatus.Open, "Only an open finding can be risk-accepted.");
            RequireText(justification, "Accepting the risk requires a documented justification.");

            Status = FindingStatus.RiskAccepted;
            Resolution = justification.Trim();
        }

        public void Close() {
            if (Status is not (FindingStatus.Resolved or FindingStatus.RiskAccepted)) {
                throw new DomainRuleException(
                    "A finding must be resolved or risk-accepted before it can be closed.");
            }

            Status = FindingStatus.Closed;
        }

        private void EnsureStatus(FindingStatus expected, string message) {
            if (Status != expected) {
                throw new DomainRuleException(message);
            }
        }

        private static void RequireText(string value, string message) {
            if (string.IsNullOrWhiteSpace(value)) {
                throw new DomainRuleException(message);
            }
        }
    }
}
