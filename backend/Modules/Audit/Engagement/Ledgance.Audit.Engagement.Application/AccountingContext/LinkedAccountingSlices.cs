using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.AccountingContext {
    public sealed record LinkedAccountingPeriod(
        Guid Id,
        string Name,
        DateOnly StartDate,
        DateOnly EndDate,
        string Status);

    public sealed record LinkedAccountingEntity(
        Guid Id,
        string Name,
        string BaseCurrency,
        IReadOnlyList<LinkedAccountingPeriod> Periods);

    public sealed record LinkedTrialBalance(
        string EntityName,
        string PeriodName,
        DateOnly AsOf,
        IReadOnlyList<TrialBalanceLine> Lines);

    public sealed record LinkedAccountingAvailability(
        bool IsAvailable,
        string? UnavailableReason);

    /// <summary>
    /// Audit's port to the organization's own Ledgance Accounting books, expressed in Audit
    /// vocabulary. Implementations must re-verify, on every call, that both products entitle
    /// context sharing and that the organization has enabled the link — Audit never assumes
    /// availability. The external-file source remains the baseline; this port is optional.
    /// </summary>
    public interface ILinkedAccountingSource {
        Task<LinkedAccountingAvailability> GetAvailabilityAsync(CancellationToken ct);
        Task<IReadOnlyList<LinkedAccountingEntity>> ListEntitiesAsync(CancellationToken ct);
        Task<LinkedTrialBalance?> GetTrialBalanceAsync(Guid accountingEntityId,
            Guid accountingPeriodId, CancellationToken ct);
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetLinkedAccountingContextQuery
        : IQuery<Result<LinkedAccountingContextView>> { }

    public class LinkedAccountingContextView {
        public bool IsAvailable { get; set; }
        public string? UnavailableReason { get; set; }
        public List<LinkedAccountingEntity> Entities { get; set; } = [];
    }

    public class GetLinkedAccountingContextQueryHandler
        : IRequestHandler<GetLinkedAccountingContextQuery,
            Result<LinkedAccountingContextView>> {
        private readonly ILinkedAccountingSource _linked;

        public GetLinkedAccountingContextQueryHandler(ILinkedAccountingSource linked) {
            _linked = linked;
        }

        public async Task<Result<LinkedAccountingContextView>> HandleAsync(
            GetLinkedAccountingContextQuery request, CancellationToken ct) {
            var availability = await _linked.GetAvailabilityAsync(ct);

            if (!availability.IsAvailable) {
                return Result<LinkedAccountingContextView>.Success(
                    new LinkedAccountingContextView {
                        IsAvailable = false,
                        UnavailableReason = availability.UnavailableReason
                    });
            }

            var entities = await _linked.ListEntitiesAsync(ct);

            return Result<LinkedAccountingContextView>.Success(
                new LinkedAccountingContextView {
                    IsAvailable = true,
                    Entities = entities.ToList()
                });
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class ImportTrialBalanceFromAccountingCommand
        : ICommand<Result<ImportTrialBalanceResult>> {
        public Guid EngagementId { get; set; }
        public Guid AccountingEntityId { get; set; }
        public Guid AccountingPeriodId { get; set; }
    }

    public class ImportTrialBalanceFromAccountingCommandValidator
        : AbstractValidator<ImportTrialBalanceFromAccountingCommand> {
        public ImportTrialBalanceFromAccountingCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.AccountingEntityId).NotEmpty();
            RuleFor(x => x.AccountingPeriodId).NotEmpty();
        }
    }

    public class ImportTrialBalanceFromAccountingCommandHandler
        : IRequestHandler<ImportTrialBalanceFromAccountingCommand,
            Result<ImportTrialBalanceResult>> {
        private readonly ITrialBalanceRepository _trialBalances;
        private readonly ILinkedAccountingSource _linked;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ImportTrialBalanceFromAccountingCommandHandler(
            ITrialBalanceRepository trialBalances, ILinkedAccountingSource linked,
            IEngagementAccessGuard access, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _trialBalances = trialBalances;
            _linked = linked;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<ImportTrialBalanceResult>> HandleAsync(
            ImportTrialBalanceFromAccountingCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var trialBalance = await _linked.GetTrialBalanceAsync(request.AccountingEntityId,
                request.AccountingPeriodId, ct);

            if (trialBalance is null) {
                return Result<ImportTrialBalanceResult>.Error(
                    "The accounting entity or fiscal period was not found in Ledgance Accounting.");
            }

            if (trialBalance.Lines.Count == 0) {
                return Result<ImportTrialBalanceResult>.Error(
                    "The selected period has no posted activity to import.");
            }

            var import = TrialBalanceImport.Create(request.EngagementId,
                TrialBalanceSource.LedganceAccounting,
                $"{trialBalance.PeriodName} (as of {trialBalance.AsOf:yyyy-MM-dd})",
                trialBalance.Lines, _currentUser.Require().UserId);

            await _trialBalances.AddAsync(import, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "trial_balance.imported",
                "TrialBalance", import.Id,
                $"A trial balance ({import.Lines.Count} lines, {(import.IsBalanced ? "balanced" : "OUT OF BALANCE")}) " +
                $"was imported from Ledgance Accounting — entity '{trialBalance.EntityName}', " +
                $"period '{trialBalance.PeriodName}' as of {trialBalance.AsOf:yyyy-MM-dd}.",
                request.EngagementId), ct);

            return Result<ImportTrialBalanceResult>.Success(new ImportTrialBalanceResult {
                ImportId = import.Id,
                LineCount = import.Lines.Count,
                TotalDebits = import.TotalDebits,
                TotalCredits = import.TotalCredits,
                IsBalanced = import.IsBalanced
            });
        }
    }
}
