using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Attributes;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using System.Reflection;

namespace Ledgance.Shared.Infrastructure.Behaviors {
    /// <summary>
    /// Default-deny: a request without <see cref="AllowAnonymousRequestAttribute"/> requires an
    /// authenticated caller with an organization context, regardless of transport-level checks.
    /// </summary>
    [PipelineOrder(100)]
    public sealed class AuthorizationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull {
        private static readonly bool AllowsAnonymous =
            typeof(TRequest).GetCustomAttribute<AllowAnonymousRequestAttribute>() is not null;

        private static readonly string[] RequiredPermissions =
            typeof(TRequest).GetCustomAttributes<RequiresPermissionAttribute>()
                .Select(attribute => attribute.Permission)
                .ToArray();

        private readonly ICurrentUserAccessor _currentUser;

        public AuthorizationBehavior(ICurrentUserAccessor currentUser) {
            _currentUser = currentUser;
        }

        public Task<TResponse> HandleAsync(TRequest request,
            RequestHandlerDelegate<TResponse> next, CancellationToken ct) {
            if (AllowsAnonymous) {
                return next();
            }

            var user = _currentUser.Require();

            foreach (var permission in RequiredPermissions) {
                if (!user.HasPermission(permission)) {
                    throw ForbiddenException.MissingPermission(permission);
                }
            }

            return next();
        }
    }
}
