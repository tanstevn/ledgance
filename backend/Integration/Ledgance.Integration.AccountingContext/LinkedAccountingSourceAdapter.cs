using Ledgance.Accounting.Ledger.Application.Published;
using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Integration.AccountingContext {
    /// <summary>
    /// Implements Audit's <see cref="ILinkedAccountingSource"/> against Accounting's
    /// published read contract. Availability — both products entitled to context sharing and
    /// the organization link enabled — is re-verified on every call, never cached across
    /// requests. When the monolith splits, only this adapter changes (module-boundaries §6).
    /// </summary>
    public sealed class LinkedAccountingSourceAdapter : ILinkedAccountingSource {
        private readonly IAccountingReadContract _accounting;
        private readonly IAccountingLinkStore _link;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;

        public LinkedAccountingSourceAdapter(IAccountingReadContract accounting,
            IAccountingLinkStore link, IEntitlementService entitlements,
            ICurrentUserAccessor currentUser) {
            _accounting = accounting;
            _link = link;
            _entitlements = entitlements;
            _currentUser = currentUser;
        }

        public async Task<LinkedAccountingAvailability> GetAvailabilityAsync(
            CancellationToken ct) {
            var organizationId = _currentUser.Require().OrganizationId;

            if (!(await _entitlements.GetAsync(organizationId, ProductModule.Audit, ct))
                    .Has(Entitlements.AccountingContextSharing)) {
                return new LinkedAccountingAvailability(false,
                    "Accounting context sharing is not included in the organization's Audit plan.");
            }

            if (!(await _entitlements.GetAsync(organizationId, ProductModule.Accounting, ct))
                    .Has(Entitlements.AccountingContextSharing)) {
                return new LinkedAccountingAvailability(false,
                    "Accounting context sharing is not included in the organization's Accounting plan.");
            }

            if (!await _link.IsEnabledAsync(ct)) {
                return new LinkedAccountingAvailability(false,
                    "The organization has not enabled Audit access to its Ledgance Accounting books.");
            }

            return new LinkedAccountingAvailability(true, null);
        }

        public async Task<IReadOnlyList<LinkedAccountingEntity>> ListEntitiesAsync(
            CancellationToken ct) {
            await EnsureAvailableAsync(ct);

            var entities = await _accounting.ListEntitiesAsync(ct);
            var result = new List<LinkedAccountingEntity>();

            foreach (var entity in entities.Where(entity => !entity.IsArchived)) {
                var periods = await _accounting.ListPeriodsAsync(entity.Id, ct);

                result.Add(new LinkedAccountingEntity(entity.Id, entity.Name,
                    entity.BaseCurrency, periods
                        .Select(period => new LinkedAccountingPeriod(period.Id, period.Name,
                            period.StartDate, period.EndDate, period.Status))
                        .ToList()));
            }

            return result;
        }

        public async Task<LinkedTrialBalance?> GetTrialBalanceAsync(Guid accountingEntityId,
            Guid accountingPeriodId, CancellationToken ct) {
            await EnsureAvailableAsync(ct);

            var snapshot = await _accounting.GetTrialBalanceAsync(accountingEntityId,
                accountingPeriodId, ct);

            if (snapshot is null) {
                return null;
            }

            var entities = await _accounting.ListEntitiesAsync(ct);
            var entityName = entities
                .FirstOrDefault(entity => entity.Id == accountingEntityId)?.Name
                ?? "Unknown entity";

            return new LinkedTrialBalance(entityName, snapshot.PeriodName, snapshot.AsOf,
                snapshot.Lines
                    .Select(line => new TrialBalanceLine(line.AccountCode, line.AccountName,
                        line.Debit, line.Credit))
                    .ToList());
        }

        private async Task EnsureAvailableAsync(CancellationToken ct) {
            var organizationId = _currentUser.Require().OrganizationId;

            (await _entitlements.GetAsync(organizationId, ProductModule.Audit, ct))
                .RequireCapability(Entitlements.AccountingContextSharing);
            (await _entitlements.GetAsync(organizationId, ProductModule.Accounting, ct))
                .RequireCapability(Entitlements.AccountingContextSharing);

            if (!await _link.IsEnabledAsync(ct)) {
                throw new DomainRuleException(
                    "The organization has not enabled Audit access to its Ledgance Accounting books.");
            }
        }
    }
}
