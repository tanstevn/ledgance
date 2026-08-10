using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.Activity {
    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetEntityActivityQuery : IQuery<Result<IEnumerable<ActivityRow>>> {
        public Guid EntityId { get; set; }
        public int Limit { get; set; } = 50;
    }

    public class ActivityRow {
        public Guid Id { get; set; }
        public string Action { get; set; } = string.Empty;
        public string SubjectType { get; set; } = string.Empty;
        public Guid SubjectId { get; set; }
        public string Summary { get; set; } = string.Empty;
        public Guid ActorUserId { get; set; }
        public string ActorEmail { get; set; } = string.Empty;
        public DateTime OccurredAt { get; set; }
    }

    public class GetEntityActivityQueryValidator : AbstractValidator<GetEntityActivityQuery> {
        public GetEntityActivityQueryValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.Limit).InclusiveBetween(1, 200);
        }
    }

    public class GetEntityActivityQueryHandler
        : IRequestHandler<GetEntityActivityQuery, Result<IEnumerable<ActivityRow>>> {
        private readonly IActivityReader _activity;
        private readonly IEntityGuard _guard;

        public GetEntityActivityQueryHandler(IActivityReader activity, IEntityGuard guard) {
            _activity = activity;
            _guard = guard;
        }

        public async Task<Result<IEnumerable<ActivityRow>>> HandleAsync(
            GetEntityActivityQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var entries = await _activity.ListAsync(request.EntityId, request.Limit, ct);

            return Result<IEnumerable<ActivityRow>>.Success(entries
                .Select(entry => new ActivityRow {
                    Id = entry.Id,
                    Action = entry.Action,
                    SubjectType = entry.SubjectType,
                    SubjectId = entry.SubjectId,
                    Summary = entry.Summary,
                    ActorUserId = entry.ActorUserId,
                    ActorEmail = entry.ActorEmail,
                    OccurredAt = entry.OccurredAt
                }));
        }
    }
}
