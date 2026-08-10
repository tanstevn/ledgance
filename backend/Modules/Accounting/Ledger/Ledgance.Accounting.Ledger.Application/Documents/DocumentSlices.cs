using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.Ledger.Application.Documents {
    [RequiresPermission(AccountingLedgerPermissions.Contribute)]
    public class UploadDocumentCommand : ICommand<Result<Guid>> {
        public Guid EntityId { get; set; }
        public Guid? JournalEntryId { get; set; }
        public Guid? ReconciliationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public byte[] Content { get; set; } = [];
    }

    public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand> {
        public const long MaxFileSizeBytes = 25 * 1024 * 1024;

        public UploadDocumentCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
            RuleFor(x => x.Description).MaximumLength(500);
            RuleFor(x => x.Content).NotEmpty()
                .WithMessage("The document file is empty.");
            RuleFor(x => x.Content.LongLength).LessThanOrEqualTo(MaxFileSizeBytes)
                .WithMessage("Documents are limited to 25 MB.");
        }
    }

    public class UploadDocumentCommandHandler
        : IRequestHandler<UploadDocumentCommand, Result<Guid>> {
        private readonly IDocumentRepository _documents;
        private readonly IDocumentFileStore _files;
        private readonly IEntityGuard _guard;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public UploadDocumentCommandHandler(IDocumentRepository documents,
            IDocumentFileStore files, IEntityGuard guard, IEntitlementService entitlements,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _documents = documents;
            _files = files;
            _guard = guard;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(UploadDocumentCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var user = _currentUser.Require();
            var entitlements = await _entitlements.GetAsync(user.OrganizationId,
                ProductModule.Accounting, ct);

            var usedBytes = await _documents.SumSizeBytesAsync(ct);
            entitlements.RequireWithinLimit(Entitlements.StorageBytes,
                usedBytes + request.Content.LongLength);

            var documentId = Guid.NewGuid();
            var path = await _files.UploadAsync(request.EntityId, documentId,
                request.FileName, request.Content, request.ContentType, ct);

            var document = AccountingDocument.Upload(request.EntityId,
                request.JournalEntryId, request.ReconciliationId, request.FileName,
                request.ContentType, request.Content.LongLength, path, request.Description,
                user.UserId);

            await _documents.AddAsync(document, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "document.uploaded",
                "Document", document.Id,
                $"Document '{document.FileName}' was uploaded.", request.EntityId), ct);

            return Result<Guid>.Success(document.Id);
        }
    }

    public class DocumentRow {
        public Guid Id { get; set; }
        public Guid? JournalEntryId { get; set; }
        public Guid? ReconciliationId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public long SizeBytes { get; set; }
        public string Description { get; set; } = string.Empty;
        public Guid UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetDocumentsQuery : IQuery<Result<IEnumerable<DocumentRow>>> {
        public Guid EntityId { get; set; }
        public Guid? JournalEntryId { get; set; }
        public Guid? ReconciliationId { get; set; }
    }

    public class GetDocumentsQueryHandler
        : IRequestHandler<GetDocumentsQuery, Result<IEnumerable<DocumentRow>>> {
        private readonly IDocumentRepository _documents;
        private readonly IEntityGuard _guard;

        public GetDocumentsQueryHandler(IDocumentRepository documents, IEntityGuard guard) {
            _documents = documents;
            _guard = guard;
        }

        public async Task<Result<IEnumerable<DocumentRow>>> HandleAsync(
            GetDocumentsQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var documents = await _documents.ListAsync(request.EntityId,
                request.JournalEntryId, request.ReconciliationId, ct);

            return Result<IEnumerable<DocumentRow>>.Success(documents
                .OrderByDescending(document => document.UploadedAt)
                .Select(document => new DocumentRow {
                    Id = document.Id,
                    JournalEntryId = document.JournalEntryId,
                    ReconciliationId = document.ReconciliationId,
                    FileName = document.FileName,
                    ContentType = document.ContentType,
                    SizeBytes = document.SizeBytes,
                    Description = document.Description,
                    UploadedBy = document.UploadedBy,
                    UploadedAt = document.UploadedAt
                }));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetDocumentDownloadUrlQuery : IQuery<Result<string>> {
        public Guid EntityId { get; set; }
        public Guid DocumentId { get; set; }
    }

    public class GetDocumentDownloadUrlQueryHandler
        : IRequestHandler<GetDocumentDownloadUrlQuery, Result<string>> {
        private readonly IDocumentRepository _documents;
        private readonly IDocumentFileStore _files;
        private readonly IEntityGuard _guard;

        public GetDocumentDownloadUrlQueryHandler(IDocumentRepository documents,
            IDocumentFileStore files, IEntityGuard guard) {
            _documents = documents;
            _files = files;
            _guard = guard;
        }

        public async Task<Result<string>> HandleAsync(GetDocumentDownloadUrlQuery request,
            CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var document = await _documents.FindAsync(request.DocumentId, ct);

            if (document is null || document.EntityId != request.EntityId) {
                return Result<string>.Error("The document was not found.");
            }

            var url = await _files.CreateDownloadUrlAsync(document.StoragePath,
                TimeSpan.FromMinutes(10), ct);

            return Result<string>.Success(url);
        }
    }
}
