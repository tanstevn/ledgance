using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.Activity {
    /// <summary>
    /// The organization's recent Accounting activity. Accounting access is organization-role
    /// based — no per-record confinement — so the module feed is returned as-is.
    /// </summary>
    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetRecentAccountingActivityQuery
        : IQuery<Result<IEnumerable<ActivityRow>>> {
        public int Limit { get; set; } = 10;
    }

    public class GetRecentAccountingActivityQueryValidator
        : AbstractValidator<GetRecentAccountingActivityQuery> {
        public GetRecentAccountingActivityQueryValidator() {
            RuleFor(x => x.Limit).InclusiveBetween(1, 50);
        }
    }

    public class GetRecentAccountingActivityQueryHandler
        : IRequestHandler<GetRecentAccountingActivityQuery,
            Result<IEnumerable<ActivityRow>>> {
        private readonly IActivityReader _activity;

        public GetRecentAccountingActivityQueryHandler(IActivityReader activity) {
            _activity = activity;
        }

        public async Task<Result<IEnumerable<ActivityRow>>> HandleAsync(
            GetRecentAccountingActivityQuery request, CancellationToken ct) {
            var entries = await _activity.ListRecentAsync("Accounting", request.Limit, ct);

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
