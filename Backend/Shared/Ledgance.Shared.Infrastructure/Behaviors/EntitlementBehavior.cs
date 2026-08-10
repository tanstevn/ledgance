using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Attributes;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using System.Reflection;

namespace Ledgance.Shared.Infrastructure.Behaviors {
    [PipelineOrder(200)]
    public sealed class EntitlementBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull {
        private static readonly RequiresEntitlementAttribute[] Required =
            typeof(TRequest).GetCustomAttributes<RequiresEntitlementAttribute>()
                .ToArray();

        private readonly ICurrentUserAccessor _currentUser;
        private readonly IEntitlementService _entitlements;

        public EntitlementBehavior(ICurrentUserAccessor currentUser,
            IEntitlementService entitlements) {
            _currentUser = currentUser;
            _entitlements = entitlements;
        }

        public async Task<TResponse> HandleAsync(TRequest request,
            RequestHandlerDelegate<TResponse> next, CancellationToken ct) {
            if (Required.Length == 0) {
                return await next();
            }

            var organizationId = _currentUser.RequireOrganizationId();

            foreach (var requirement in Required) {
                var entitlements = await _entitlements
                    .GetAsync(organizationId, requirement.Module, ct);

                entitlements.RequireCapability(requirement.Capability);
            }

            return await next();
        }
    }
}
