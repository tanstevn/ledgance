using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Integration.AccountingContext {
    /// <summary>
    /// The per-organization opt-in that authorizes Audit to read the organization's own
    /// Ledgance Accounting books. Off by default; enabling requires the
    /// accounting_context_sharing entitlement on both products.
    /// </summary>
    public interface IAccountingLinkStore {
        Task<bool> IsEnabledAsync(CancellationToken ct);
        Task SetEnabledAsync(bool enabled, CancellationToken ct);
    }

    [RequiresPermission(AccountingLinkPermissions.Manage)]
    public class SetAccountingLinkCommand : ICommand<Result<bool>> {
        public bool Enabled { get; set; }
    }

    public class SetAccountingLinkCommandValidator
        : AbstractValidator<SetAccountingLinkCommand> { }

    public class SetAccountingLinkCommandHandler
        : IRequestHandler<SetAccountingLinkCommand, Result<bool>> {
        private readonly IAccountingLinkStore _link;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public SetAccountingLinkCommandHandler(IAccountingLinkStore link,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _link = link;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(SetAccountingLinkCommand request,
            CancellationToken ct) {
            var organizationId = _currentUser.Require().OrganizationId;

            if (request.Enabled) {
                (await _entitlements.GetAsync(organizationId, ProductModule.Audit, ct))
                    .RequireCapability(Entitlements.AccountingContextSharing);
                (await _entitlements.GetAsync(organizationId, ProductModule.Accounting, ct))
                    .RequireCapability(Entitlements.AccountingContextSharing);
            }

            await _link.SetEnabledAsync(request.Enabled, ct);

            var action = request.Enabled
                ? "accounting_link.enabled"
                : "accounting_link.disabled";
            var summary = request.Enabled
                ? "enabled Audit access to the organization's Ledgance Accounting books."
                : "disabled Audit access to the organization's Ledgance Accounting books.";

            await _activity.RecordAsync(new ActivityEntry("Integration", action,
                "Organization", organizationId, summary), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLinkPermissions.Read)]
    public class GetAccountingLinkStatusQuery : IQuery<Result<AccountingLinkStatusView>> { }

    public class AccountingLinkStatusView {
        public bool LinkEnabled { get; set; }
        public bool AuditPlanIncludesSharing { get; set; }
        public bool AccountingPlanIncludesSharing { get; set; }
        public bool IsActive { get; set; }
    }

    public class GetAccountingLinkStatusQueryHandler
        : IRequestHandler<GetAccountingLinkStatusQuery, Result<AccountingLinkStatusView>> {
        private readonly IAccountingLinkStore _link;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;

        public GetAccountingLinkStatusQueryHandler(IAccountingLinkStore link,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser) {
            _link = link;
            _entitlements = entitlements;
            _currentUser = currentUser;
        }

        public async Task<Result<AccountingLinkStatusView>> HandleAsync(
            GetAccountingLinkStatusQuery request, CancellationToken ct) {
            var organizationId = _currentUser.Require().OrganizationId;

            var enabled = await _link.IsEnabledAsync(ct);
            var auditEntitled = (await _entitlements.GetAsync(organizationId,
                ProductModule.Audit, ct)).Has(Entitlements.AccountingContextSharing);
            var accountingEntitled = (await _entitlements.GetAsync(organizationId,
                ProductModule.Accounting, ct)).Has(Entitlements.AccountingContextSharing);

            return Result<AccountingLinkStatusView>.Success(new AccountingLinkStatusView {
                LinkEnabled = enabled,
                AuditPlanIncludesSharing = auditEntitled,
                AccountingPlanIncludesSharing = accountingEntitled,
                IsActive = enabled && auditEntitled && accountingEntitled
            });
        }
    }
}
