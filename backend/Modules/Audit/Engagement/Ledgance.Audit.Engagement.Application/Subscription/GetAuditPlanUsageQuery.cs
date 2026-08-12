using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.Engagement.Application.Subscription {
    /// <summary>
    /// What the organization is actually using against its Audit plan. The billing page needs
    /// this to say "42 of 250 clients" rather than only naming the ceiling, and it comes from
    /// the server so the numbers shown are the numbers enforced.
    /// </summary>
    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetAuditPlanUsageQuery : IQuery<Result<AuditPlanUsage>> { }

    public class AuditPlanUsage {
        public string Plan { get; set; } = string.Empty;

        /// <summary>
        /// When the AI allowance refills. It follows the paid billing period where there is one,
        /// so the date shown is the date the customer is charged.
        /// </summary>
        public DateTime? AiPeriodResetsAt { get; set; }

        public List<PlanUsageMeasure> Measures { get; set; } = [];
    }

    /// <summary>
    /// One metered dimension. <see cref="Limit"/> is -1 for unlimited, matching the entitlement
    /// value, so the client never has to know a second convention.
    /// </summary>
    public class PlanUsageMeasure {
        public string Key { get; set; } = string.Empty;
        public long Used { get; set; }
        public long Limit { get; set; }
    }

    public class GetAuditPlanUsageQueryHandler
        : IRequestHandler<GetAuditPlanUsageQuery, Result<AuditPlanUsage>> {
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IEvidenceRepository _evidence;
        private readonly IOrganizationDirectory _organizations;
        private readonly IEntitlementService _entitlements;
        private readonly IAiUsageMeter _aiUsage;
        private readonly IAiUsagePeriodResolver _aiPeriods;
        private readonly ICurrentUserAccessor _currentUser;

        public GetAuditPlanUsageQueryHandler(IEngagementRepository engagements,
            IClientLookup clients, IEvidenceRepository evidence,
            IOrganizationDirectory organizations, IEntitlementService entitlements,
            IAiUsageMeter aiUsage, IAiUsagePeriodResolver aiPeriods,
            ICurrentUserAccessor currentUser) {
            _engagements = engagements;
            _clients = clients;
            _evidence = evidence;
            _organizations = organizations;
            _entitlements = entitlements;
            _aiUsage = aiUsage;
            _aiPeriods = aiPeriods;
            _currentUser = currentUser;
        }

        public async Task<Result<AuditPlanUsage>> HandleAsync(GetAuditPlanUsageQuery request,
            CancellationToken ct) {
            var organizationId = _currentUser.RequireOrganizationId();

            var entitlementsTask = _entitlements.GetAsync(organizationId, ProductModule.Audit, ct);
            var periodTask = _aiPeriods.ResolveAsync(organizationId, ProductModule.Audit, ct);
            var engagementsTask = _engagements.CountActiveAsync(ct);
            var clientsTask = _clients.CountActiveAsync(ct);
            var storageTask = _evidence.SumSizeBytesAsync(ct);
            var membersTask = _organizations.ListMembersAsync(organizationId, ct);

            await Task.WhenAll(entitlementsTask, periodTask, engagementsTask, clientsTask,
                storageTask, membersTask);

            var entitlements = entitlementsTask.Result;
            var period = periodTask.Result;

            var ai = await _aiUsage.GetAsync(organizationId, ProductModule.Audit, period,
                entitlements.Limit(Entitlements.AiMonthlyUnits), ct);

            return Result<AuditPlanUsage>.Success(new AuditPlanUsage {
                Plan = entitlements.Plan.ToString(),
                AiPeriodResetsAt = period.ResetsAt,
                Measures = [
                    Measure(Entitlements.MaxUsers, membersTask.Result.Count, entitlements),
                    Measure(Entitlements.MaxClients, clientsTask.Result, entitlements),
                    Measure(Entitlements.MaxEngagements, engagementsTask.Result, entitlements),
                    Measure(Entitlements.StorageBytes, storageTask.Result, entitlements),
                    new PlanUsageMeasure {
                        Key = Entitlements.AiMonthlyUnits,
                        Used = ai.Used,
                        Limit = ai.Limit
                    }
                ]
            });
        }

        private static PlanUsageMeasure Measure(string key, long used,
            EntitlementSet entitlements) =>
            new() { Key = key, Used = used, Limit = entitlements.Limit(key) };
    }
}
