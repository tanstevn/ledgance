using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Accounting.Ledger.Application.Periods {
    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class CreateFiscalPeriodCommand : ICommand<Result<Guid>> {
        public Guid EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
    }

    public class CreateFiscalPeriodCommandValidator
        : AbstractValidator<CreateFiscalPeriodCommand> {
        public CreateFiscalPeriodCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.StartDate).NotEmpty();
            RuleFor(x => x.EndDate).NotEmpty();
        }
    }

    public class CreateFiscalPeriodCommandHandler
        : IRequestHandler<CreateFiscalPeriodCommand, Result<Guid>> {
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public CreateFiscalPeriodCommandHandler(IFiscalPeriodRepository periods,
            IEntityGuard guard, IActivityRecorder activity) {
            _periods = periods;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(CreateFiscalPeriodCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var existing = await _periods.ListAsync(request.EntityId, ct);

            if (existing.Any(period => period.Overlaps(request.StartDate, request.EndDate))) {
                return Result<Guid>.Error(
                    "The period overlaps an existing fiscal period.");
            }

            var period = FiscalPeriod.Open(request.EntityId, request.Name, request.StartDate,
                request.EndDate);

            await _periods.AddAsync(period, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "period.created",
                "FiscalPeriod", period.Id,
                $"Fiscal period '{period.Name}' ({period.StartDate:yyyy-MM-dd} – {period.EndDate:yyyy-MM-dd}) was created.",
                request.EntityId), ct);

            return Result<Guid>.Success(period.Id);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class CloseFiscalPeriodCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class CloseFiscalPeriodCommandValidator
        : AbstractValidator<CloseFiscalPeriodCommand> {
        public CloseFiscalPeriodCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class CloseFiscalPeriodCommandHandler
        : IRequestHandler<CloseFiscalPeriodCommand, Result<bool>> {
        private readonly IFiscalPeriodRepository _periods;
        private readonly IJournalEntryRepository _entries;
        private readonly IEntityGuard _guard;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public CloseFiscalPeriodCommandHandler(IFiscalPeriodRepository periods,
            IJournalEntryRepository entries, IEntityGuard guard,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _periods = periods;
            _entries = entries;
            _guard = guard;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(CloseFiscalPeriodCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<bool>.Error("The fiscal period was not found.");
            }

            var hasDrafts = await _entries.HasDraftsInRangeAsync(request.EntityId,
                period.StartDate, period.EndDate, ct);

            period.Close(hasDrafts, _currentUser.Require().UserId);
            await _periods.UpdateAsync(period, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "period.closed",
                "FiscalPeriod", period.Id,
                $"Fiscal period '{period.Name}' was closed.", request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class ReopenFiscalPeriodCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public Guid PeriodId { get; set; }
    }

    public class ReopenFiscalPeriodCommandValidator
        : AbstractValidator<ReopenFiscalPeriodCommand> {
        public ReopenFiscalPeriodCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.PeriodId).NotEmpty();
        }
    }

    public class ReopenFiscalPeriodCommandHandler
        : IRequestHandler<ReopenFiscalPeriodCommand, Result<bool>> {
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public ReopenFiscalPeriodCommandHandler(IFiscalPeriodRepository periods,
            IEntityGuard guard, IActivityRecorder activity) {
            _periods = periods;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(ReopenFiscalPeriodCommand request,
            CancellationToken ct) {
            await _guard.RequireActiveAsync(request.EntityId, ct);

            var period = await _periods.FindAsync(request.PeriodId, ct);

            if (period is null || period.EntityId != request.EntityId) {
                return Result<bool>.Error("The fiscal period was not found.");
            }

            period.Reopen();
            await _periods.UpdateAsync(period, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "period.reopened",
                "FiscalPeriod", period.Id,
                $"Fiscal period '{period.Name}' was reopened.", request.EntityId), ct);

            return Result<bool>.Success(true);
        }
    }

    public class FiscalPeriodRow {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? ClosedBy { get; set; }
        public DateTime? ClosedAt { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetFiscalPeriodsQuery : IQuery<Result<IEnumerable<FiscalPeriodRow>>> {
        public Guid EntityId { get; set; }
    }

    public class GetFiscalPeriodsQueryHandler
        : IRequestHandler<GetFiscalPeriodsQuery, Result<IEnumerable<FiscalPeriodRow>>> {
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;

        public GetFiscalPeriodsQueryHandler(IFiscalPeriodRepository periods,
            IEntityGuard guard) {
            _periods = periods;
            _guard = guard;
        }

        public async Task<Result<IEnumerable<FiscalPeriodRow>>> HandleAsync(
            GetFiscalPeriodsQuery request, CancellationToken ct) {
            await _guard.RequireAsync(request.EntityId, ct);

            var periods = await _periods.ListAsync(request.EntityId, ct);

            return Result<IEnumerable<FiscalPeriodRow>>.Success(periods
                .OrderBy(period => period.StartDate)
                .Select(period => new FiscalPeriodRow {
                    Id = period.Id,
                    Name = period.Name,
                    StartDate = period.StartDate,
                    EndDate = period.EndDate,
                    Status = period.Status.ToString(),
                    ClosedBy = period.ClosedBy,
                    ClosedAt = period.ClosedAt
                }));
        }
    }
}
