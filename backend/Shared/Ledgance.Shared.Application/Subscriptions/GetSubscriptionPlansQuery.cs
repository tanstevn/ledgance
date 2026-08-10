using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Shared.Application.Subscriptions {
    /// <summary>
    /// The public plan catalog: anonymous because pricing pages render before sign-in. It
    /// exposes only what the catalog already declares — entitlement values, never secrets —
    /// so marketing surfaces and the application stay consistent with the same source of
    /// truth the backend enforces.
    /// </summary>
    [AllowAnonymousRequest]
    public class GetSubscriptionPlansQuery
        : IQuery<Result<IEnumerable<SubscriptionPlanRow>>> { }

    public class SubscriptionPlanRow {
        public string Code { get; set; } = string.Empty;
        public string Module { get; set; } = string.Empty;
        public bool IsFree { get; set; }
        public bool RequiresContactSales { get; set; }
        public Dictionary<string, string> Entitlements { get; set; } = [];
    }

    public class GetSubscriptionPlansQueryHandler
        : IRequestHandler<GetSubscriptionPlansQuery, Result<IEnumerable<SubscriptionPlanRow>>> {
        public Task<Result<IEnumerable<SubscriptionPlanRow>>> HandleAsync(
            GetSubscriptionPlansQuery request, CancellationToken ct) =>
            Task.FromResult(Result<IEnumerable<SubscriptionPlanRow>>.Success(
                SubscriptionPlanCatalog.All.Select(plan => new SubscriptionPlanRow {
                    Code = plan.Key.ToString(),
                    Module = SubscriptionPlanCatalog.ModuleOf(plan.Key).ToString(),
                    IsFree = plan.Key == PlanCode.Free,
                    RequiresContactSales =
                        SubscriptionPlanCatalog.RequiresContactSales(plan.Key),
                    Entitlements = plan.Value.ToDictionary(entry => entry.Key,
                        entry => entry.Value)
                })));
    }
}
