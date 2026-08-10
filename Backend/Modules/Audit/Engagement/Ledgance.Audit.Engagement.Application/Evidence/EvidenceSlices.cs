using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using DomainEvidence = Ledgance.Audit.Engagement.Domain.Evidence;

namespace Ledgance.Audit.Engagement.Application.Evidence {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class UploadEvidenceCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public Guid? WorkingPaperId { get; set; }
        public Guid? ProcedureId { get; set; }
        public Guid? SupersedesEvidenceId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte[] Content { get; set; } = [];
    }

    public class UploadEvidenceCommandValidator : AbstractValidator<UploadEvidenceCommand> {
        public const long MaxFileSizeBytes = 25 * 1024 * 1024;

        public UploadEvidenceCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Content).NotEmpty()
                .WithMessage("The evidence file is empty.");
            RuleFor(x => x.Content.LongLength).LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage("Evidence files are limited to 25 MB.");
        }
    }

    public class UploadEvidenceCommandHandler
        : IRequestHandler<UploadEvidenceCommand, Result<Guid>> {
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceFileStore _files;
        private readonly IEngagementAccessGuard _access;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public UploadEvidenceCommandHandler(IEvidenceRepository evidence,
            IEvidenceFileStore files, IEngagementAccessGuard access,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _evidence = evidence;
            _files = files;
            _access = access;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(UploadEvidenceCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var user = _currentUser.Require();
            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                ProductModule.Audit, ct);

            var usedBytes = await _evidence.SumSizeBytesAsync(ct);
            entitlements.RequireWithinLimit(Entitlements.StorageBytes,
                usedBytes + request.Content.LongLength);

            if (request.SupersedesEvidenceId is not null) {
                var existing = await _evidence.FindAsync(request.SupersedesEvidenceId.Value, ct);

                if (existing is null || existing.EngagementId != request.EngagementId) {
                    return Result<Guid>.Error("The evidence to supersede was not found.");
                }

                var newPath = await _files.UploadAsync(request.EngagementId, existing.Id,
                    existing.Version + 1, request.FileName, request.Content,
                    request.ContentType, ct);

                existing.Supersede(newPath, request.Content.LongLength,
                    request.ContentType, user.UserId);
                await _evidence.UpdateAsync(existing, ct);

                await _activity.RecordAsync(new ActivityEntry("Audit", "evidence.superseded",
                    "Evidence", existing.Id,
                    $"Evidence '{existing.FileName}' was superseded (v{existing.Version}).",
                    request.EngagementId), ct);

                return Result<Guid>.Success(existing.Id);
            }

            var evidenceId = Guid.NewGuid();
            var path = await _files.UploadAsync(request.EngagementId, evidenceId, 1,
                request.FileName, request.Content, request.ContentType, ct);

            var evidence = DomainEvidence.Upload(request.EngagementId, request.WorkingPaperId,
                request.ProcedureId, request.FileName, request.ContentType,
                request.Content.LongLength, path, request.Description, user.UserId);

            await _evidence.AddAsync(evidence, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "evidence.uploaded",
                "Evidence", evidence.Id, $"Evidence '{evidence.FileName}' was uploaded.",
                request.EngagementId), ct);

            return Result<Guid>.Success(evidence.Id);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetEvidenceQuery : IQuery<Result<IEnumerable<EvidenceRow>>> {
        public Guid EngagementId { get; set; }
    }

    public class EvidenceRow {
        public Guid Id { get; set; }
        public Guid? WorkingPaperId { get; set; }
        public Guid? ProcedureId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public int Version { get; set; }
        public string Description { get; set; } = string.Empty;
        public Guid UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class GetEvidenceQueryHandler
        : IRequestHandler<GetEvidenceQuery, Result<IEnumerable<EvidenceRow>>> {
        private readonly IEvidenceRepository _evidence;
        private readonly IEngagementAccessGuard _access;

        public GetEvidenceQueryHandler(IEvidenceRepository evidence,
            IEngagementAccessGuard access) {
            _evidence = evidence;
            _access = access;
        }

        public async Task<Result<IEnumerable<EvidenceRow>>> HandleAsync(
            GetEvidenceQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var evidence = await _evidence.ListAsync(request.EngagementId, ct);

            return Result<IEnumerable<EvidenceRow>>.Success(evidence
                .Select(item => new EvidenceRow {
                    Id = item.Id,
                    WorkingPaperId = item.WorkingPaperId,
                    ProcedureId = item.ProcedureId,
                    FileName = item.FileName,
                    ContentType = item.ContentType,
                    SizeBytes = item.SizeBytes,
                    Version = item.Version,
                    Description = item.Description,
                    UploadedBy = item.UploadedBy,
                    UploadedAt = item.UploadedAt
                }));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetEvidenceDownloadUrlQuery : IQuery<Result<string>> {
        public Guid EngagementId { get; set; }
        public Guid EvidenceId { get; set; }
    }

    public class GetEvidenceDownloadUrlQueryHandler
        : IRequestHandler<GetEvidenceDownloadUrlQuery, Result<string>> {
        private readonly IEvidenceRepository _evidence;
        private readonly IEvidenceFileStore _files;
        private readonly IEngagementAccessGuard _access;

        public GetEvidenceDownloadUrlQueryHandler(IEvidenceRepository evidence,
            IEvidenceFileStore files, IEngagementAccessGuard access) {
            _evidence = evidence;
            _files = files;
            _access = access;
        }

        public async Task<Result<string>> HandleAsync(GetEvidenceDownloadUrlQuery request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var evidence = await _evidence.FindAsync(request.EvidenceId, ct);

            if (evidence is null || evidence.EngagementId != request.EngagementId) {
                return Result<string>.Error("Evidence was not found.");
            }

            var url = await _files.CreateDownloadUrlAsync(evidence.StoragePath,
                TimeSpan.FromMinutes(10), ct);

            return Result<string>.Success(url);
        }
    }
}
