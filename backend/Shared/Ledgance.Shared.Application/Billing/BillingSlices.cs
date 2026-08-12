using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Shared.Application.Billing {
    internal static class BillingPlans {
        /// <summary>
        /// A plan is purchasable when it is a real paid tier with a configured price. Free needs
        /// no purchase and Enterprise is a negotiated contract, so neither reaches the provider.
        /// </summary>
        public static string? RejectionReason(PlanCode plan, IBillingPriceCatalog prices) {
            if (plan == PlanCode.Free) {
                return "The Free plan needs no subscription. Cancel a paid plan to return to it.";
            }

            if (SubscriptionPlanCatalog.RequiresContactSales(plan)) {
                return "Enterprise plans are arranged with our sales team, not through checkout.";
            }

            return prices.PriceIdFor(plan) is null
                ? "This plan is not open for online purchase yet."
                : null;
        }

        public static bool TryParse(string value, out PlanCode plan) =>
            Enum.TryParse(value, ignoreCase: true, out plan)
                && Enum.IsDefined(plan);
    }

    [RequiresPermission(SharedPermissions.BillingManage)]
    public class StartCheckoutCommand : ICommand<Result<StartCheckoutResult>> {
        public string PlanCode { get; set; } = string.Empty;
    }

    public class StartCheckoutResult {
        public string CheckoutUrl { get; set; } = string.Empty;
    }

    public class StartCheckoutCommandValidator : AbstractValidator<StartCheckoutCommand> {
        public StartCheckoutCommandValidator() {
            RuleFor(x => x.PlanCode).NotEmpty();
        }
    }

    public class StartCheckoutCommandHandler
        : IRequestHandler<StartCheckoutCommand, Result<StartCheckoutResult>> {
        private readonly IBillingGateway _billing;
        private readonly IBillingPriceCatalog _prices;
        private readonly ISubscriptionStore _subscriptions;
        private readonly IOrganizationDirectory _organizations;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IBillingUrls _urls;
        private readonly IActivityRecorder _activity;

        public StartCheckoutCommandHandler(IBillingGateway billing, IBillingPriceCatalog prices,
            ISubscriptionStore subscriptions, IOrganizationDirectory organizations,
            ICurrentUserAccessor currentUser, IBillingUrls urls, IActivityRecorder activity) {
            _billing = billing;
            _prices = prices;
            _subscriptions = subscriptions;
            _organizations = organizations;
            _currentUser = currentUser;
            _urls = urls;
            _activity = activity;
        }

        public async Task<Result<StartCheckoutResult>> HandleAsync(StartCheckoutCommand request,
            CancellationToken ct) {
            if (!BillingPlans.TryParse(request.PlanCode, out var plan)) {
                return Result<StartCheckoutResult>.Error("That plan does not exist.");
            }

            var rejection = BillingPlans.RejectionReason(plan, _prices);

            if (rejection is not null) {
                return Result<StartCheckoutResult>.Error(rejection);
            }

            var user = _currentUser.Require();
            var module = SubscriptionPlanCatalog.ModuleOf(plan);
            var existing = await _subscriptions.FindAsync(user.OrganizationId, module, ct);

            if (existing?.SubscriptionId is not null && existing.Status is
                SubscriptionStatus.Active or SubscriptionStatus.Trialing) {
                return Result<StartCheckoutResult>.Error(
                    "This product already has an active subscription. Change the plan instead.");
            }

            var organization = await _organizations.GetOrganizationAsync(user.OrganizationId, ct);

            var customerId = await _billing.EnsureCustomerAsync(user.OrganizationId,
                organization?.Name ?? "Ledgance organization", user.Email,
                existing?.CustomerId, ct);

            await _subscriptions.UpsertAsync(new StoredSubscription(
                user.OrganizationId, module, existing?.Plan ?? PlanCode.Free,
                existing?.Status ?? SubscriptionStatus.Canceled, customerId,
                existing?.SubscriptionId, existing?.CurrentPeriodEnd,
                existing?.CancelAtPeriodEnd ?? false, existing?.LastEventAt), ct);

            var url = await _billing.CreateCheckoutSessionAsync(new CheckoutRequest(
                user.OrganizationId, module, plan, _prices.PriceIdFor(plan)!, customerId,
                _urls.CheckoutSuccessUrl, _urls.CheckoutCancelUrl), ct);

            await _activity.RecordAsync(new ActivityEntry(module.ToString(), "billing.checkout",
                "Subscription", user.OrganizationId,
                $"started checkout for the {plan} plan.", user.OrganizationId), ct);

            return Result<StartCheckoutResult>.Success(new StartCheckoutResult {
                CheckoutUrl = url
            });
        }
    }

    /// <summary>
    /// Where the provider returns the customer to. Configured in Infrastructure so the
    /// Application layer holds no URLs of its own.
    /// </summary>
    public interface IBillingUrls {
        string CheckoutSuccessUrl { get; }
        string CheckoutCancelUrl { get; }
        string PortalReturnUrl { get; }
    }

    [RequiresPermission(SharedPermissions.BillingManage)]
    public class CreateBillingPortalSessionCommand : ICommand<Result<BillingPortalResult>> {
        public string Module { get; set; } = string.Empty;
    }

    public class BillingPortalResult {
        public string PortalUrl { get; set; } = string.Empty;
    }

    public class CreateBillingPortalSessionCommandHandler
        : IRequestHandler<CreateBillingPortalSessionCommand, Result<BillingPortalResult>> {
        private readonly IBillingGateway _billing;
        private readonly ISubscriptionStore _subscriptions;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IBillingUrls _urls;

        public CreateBillingPortalSessionCommandHandler(IBillingGateway billing,
            ISubscriptionStore subscriptions, ICurrentUserAccessor currentUser,
            IBillingUrls urls) {
            _billing = billing;
            _subscriptions = subscriptions;
            _currentUser = currentUser;
            _urls = urls;
        }

        public async Task<Result<BillingPortalResult>> HandleAsync(
            CreateBillingPortalSessionCommand request, CancellationToken ct) {
            if (!Enum.TryParse<ProductModule>(request.Module, ignoreCase: true, out var module)) {
                return Result<BillingPortalResult>.Error("That product does not exist.");
            }

            var user = _currentUser.Require();
            var stored = await _subscriptions.FindAsync(user.OrganizationId, module, ct);

            if (stored?.CustomerId is null) {
                return Result<BillingPortalResult>.Error(
                    "There is no billing account for this product yet.");
            }

            var url = await _billing.CreateBillingPortalSessionAsync(stored.CustomerId,
                _urls.PortalReturnUrl, ct);

            return Result<BillingPortalResult>.Success(new BillingPortalResult {
                PortalUrl = url
            });
        }
    }

    [RequiresPermission(SharedPermissions.BillingManage)]
    public class ChangeSubscriptionPlanCommand : ICommand<Result<bool>> {
        public string PlanCode { get; set; } = string.Empty;
    }

    public class ChangeSubscriptionPlanCommandValidator
        : AbstractValidator<ChangeSubscriptionPlanCommand> {
        public ChangeSubscriptionPlanCommandValidator() {
            RuleFor(x => x.PlanCode).NotEmpty();
        }
    }

    /// <summary>
    /// Upgrade or downgrade inside one product. The provider prorates; the resulting state is
    /// written straight away and confirmed again when the subscription event arrives.
    /// </summary>
    public class ChangeSubscriptionPlanCommandHandler
        : IRequestHandler<ChangeSubscriptionPlanCommand, Result<bool>> {
        private readonly IBillingGateway _billing;
        private readonly IBillingPriceCatalog _prices;
        private readonly ISubscriptionStore _subscriptions;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ChangeSubscriptionPlanCommandHandler(IBillingGateway billing,
            IBillingPriceCatalog prices, ISubscriptionStore subscriptions,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _billing = billing;
            _prices = prices;
            _subscriptions = subscriptions;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(ChangeSubscriptionPlanCommand request,
            CancellationToken ct) {
            if (!BillingPlans.TryParse(request.PlanCode, out var plan)) {
                return Result<bool>.Error("That plan does not exist.");
            }

            var rejection = BillingPlans.RejectionReason(plan, _prices);

            if (rejection is not null) {
                return Result<bool>.Error(rejection);
            }

            var user = _currentUser.Require();
            var module = SubscriptionPlanCatalog.ModuleOf(plan);
            var stored = await _subscriptions.FindAsync(user.OrganizationId, module, ct);

            if (stored?.SubscriptionId is null) {
                return Result<bool>.Error(
                    "There is no subscription to change. Start a checkout instead.");
            }

            if (stored.Plan == plan) {
                return Result<bool>.Error("That is already the current plan.");
            }

            var snapshot = await _billing.ChangePlanAsync(stored.SubscriptionId,
                _prices.PriceIdFor(plan)!, ct);

            await _subscriptions.UpsertAsync(stored with {
                Plan = plan,
                Status = snapshot.Status,
                CurrentPeriodEnd = snapshot.CurrentPeriodEnd,
                CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd
            }, ct);

            await _activity.RecordAsync(new ActivityEntry(module.ToString(),
                "billing.plan_changed", "Subscription", user.OrganizationId,
                $"changed the plan from {stored.Plan} to {plan}.", user.OrganizationId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(SharedPermissions.BillingManage)]
    public class SetSubscriptionCancellationCommand : ICommand<Result<bool>> {
        public string Module { get; set; } = string.Empty;
        public bool CancelAtPeriodEnd { get; set; } = true;
    }

    /// <summary>
    /// Cancels at the end of the paid period, or withdraws a pending cancellation. Access is
    /// never cut mid-period — the entitlement follows the provider's status.
    /// </summary>
    public class SetSubscriptionCancellationCommandHandler
        : IRequestHandler<SetSubscriptionCancellationCommand, Result<bool>> {
        private readonly IBillingGateway _billing;
        private readonly ISubscriptionStore _subscriptions;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public SetSubscriptionCancellationCommandHandler(IBillingGateway billing,
            ISubscriptionStore subscriptions, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _billing = billing;
            _subscriptions = subscriptions;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(SetSubscriptionCancellationCommand request,
            CancellationToken ct) {
            if (!Enum.TryParse<ProductModule>(request.Module, ignoreCase: true, out var module)) {
                return Result<bool>.Error("That product does not exist.");
            }

            var user = _currentUser.Require();
            var stored = await _subscriptions.FindAsync(user.OrganizationId, module, ct);

            if (stored?.SubscriptionId is null) {
                return Result<bool>.Error("There is no subscription for this product.");
            }

            var snapshot = await _billing.SetCancellationAsync(stored.SubscriptionId,
                request.CancelAtPeriodEnd, ct);

            await _subscriptions.UpsertAsync(stored with {
                Status = snapshot.Status,
                CurrentPeriodEnd = snapshot.CurrentPeriodEnd,
                CancelAtPeriodEnd = snapshot.CancelAtPeriodEnd
            }, ct);

            await _activity.RecordAsync(new ActivityEntry(module.ToString(),
                request.CancelAtPeriodEnd ? "billing.canceled" : "billing.cancellation_withdrawn",
                "Subscription", user.OrganizationId,
                request.CancelAtPeriodEnd
                    ? $"cancelled the {stored.Plan} subscription, effective at the end of the current period."
                    : $"resumed the {stored.Plan} subscription.",
                user.OrganizationId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(SharedPermissions.BillingRead)]
    public class GetBillingOverviewQuery : IQuery<Result<BillingOverview>> { }

    public class BillingOverview {
        public List<BillingProductState> Products { get; set; } = [];
    }

    public class BillingProductState {
        public string Module { get; set; } = string.Empty;
        public string Plan { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime? CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public bool HasBillingAccount { get; set; }
        public bool HasSubscription { get; set; }
        public bool RequiresContactSales { get; set; }
    }

    public class GetBillingOverviewQueryHandler
        : IRequestHandler<GetBillingOverviewQuery, Result<BillingOverview>> {
        private readonly ISubscriptionStore _subscriptions;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;

        public GetBillingOverviewQueryHandler(ISubscriptionStore subscriptions,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser) {
            _subscriptions = subscriptions;
            _entitlements = entitlements;
            _currentUser = currentUser;
        }

        public async Task<Result<BillingOverview>> HandleAsync(GetBillingOverviewQuery request,
            CancellationToken ct) {
            var organizationId = _currentUser.RequireOrganizationId();

            var states = await Task.WhenAll(Enum.GetValues<ProductModule>()
                .Select(module => LoadAsync(organizationId, module, ct)));

            return Result<BillingOverview>.Success(new BillingOverview {
                Products = [.. states]
            });
        }

        private async Task<BillingProductState> LoadAsync(Guid organizationId,
            ProductModule module, CancellationToken ct) {
            var storedTask = _subscriptions.FindAsync(organizationId, module, ct);

            var entitlementsTask = _entitlements.GetAsync(organizationId, module, ct);

            await Task.WhenAll(storedTask, entitlementsTask);

            var stored = storedTask.Result;
            var entitlements = entitlementsTask.Result;

            return new BillingProductState {
                Module = module.ToString(),
                Plan = entitlements.Plan.ToString(),
                Status = (stored?.Status ?? SubscriptionStatus.Active).ToString(),
                CurrentPeriodEnd = stored?.CurrentPeriodEnd,
                CancelAtPeriodEnd = stored?.CancelAtPeriodEnd ?? false,
                HasBillingAccount = stored?.CustomerId is not null,
                HasSubscription = stored?.SubscriptionId is not null,
                RequiresContactSales =
                    SubscriptionPlanCatalog.RequiresContactSales(entitlements.Plan)
            };
        }
    }
}
