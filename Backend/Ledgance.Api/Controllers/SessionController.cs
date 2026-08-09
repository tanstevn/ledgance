using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace Ledgance.Api.Controllers {
    public record SessionModulePlan(string Module, string Plan, bool RequiresContactSales);

    public record SessionResponse(Guid UserId, string Email, Guid? OrganizationId,
        string Role, IEnumerable<string> Permissions, IEnumerable<SessionModulePlan> Plans,
        bool NeedsOnboarding);

    /// <summary>
    /// Exposes the server-resolved identity, organization and plan context. The client uses this
    /// to render, never to decide: every gated operation is re-checked server-side.
    /// </summary>
    [Route("api/session")]
    [ApiController]
    public class SessionController : ControllerBase {
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IEntitlementService _entitlements;

        public SessionController(ICurrentUserAccessor currentUser,
            IEntitlementService entitlements) {
            _currentUser = currentUser;
            _entitlements = entitlements;
        }

        [HttpGet]
        public async Task<Result<SessionResponse>> GetSession(CancellationToken ct) {
            var user = _currentUser.Current;

            if (user is null) {
                var principal = _currentUser.RequirePrincipal();

                return Result<SessionResponse>.Success(new SessionResponse(
                    principal.UserId, principal.Email, null, string.Empty,
                    [], [], NeedsOnboarding: true));
            }

            var plans = new List<SessionModulePlan>();

            foreach (var module in Enum.GetValues<ProductModule>()) {
                var entitlements = await _entitlements.GetAsync(user.OrganizationId, module, ct);

                plans.Add(new SessionModulePlan(module.ToString(), entitlements.Plan.ToString(),
                    SubscriptionPlanCatalog.RequiresContactSales(entitlements.Plan)));
            }

            return Result<SessionResponse>.Success(new SessionResponse(
                user.UserId,
                user.Email,
                user.OrganizationId,
                user.Role.ToString(),
                user.Permissions,
                plans,
                NeedsOnboarding: false));
        }
    }
}
