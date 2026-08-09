using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Ledgance.Shared.Infrastructure.Identity {
    /// <summary>
    /// Turns a verified Supabase access token into the organization context every downstream
    /// component relies on. Membership comes from our own tables, never from a client-supplied
    /// value, so a caller cannot select the organization they operate in.
    /// </summary>
    public sealed class CurrentUserMiddleware {
        private const string OrganizationIdClaim = "org_id";
        private const string OrganizationRoleClaim = "org_role";

        private readonly RequestDelegate _next;

        public CurrentUserMiddleware(RequestDelegate next) {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context,
            ICurrentUserInitializer initializer,
            IOrganizationMembershipReader membershipReader,
            PermissionRegistry permissions) {
            var principal = context.User;

            if (principal?.Identity?.IsAuthenticated != true) {
                await _next(context);
                return;
            }

            var userId = ReadUserId(principal);
            var membership = ReadMembershipClaims(principal)
                ?? await membershipReader.FindAsync(userId, context.RequestAborted)
                ?? throw new ForbiddenException(
                    "This account is not a member of any organization.");

            initializer.Set(new CurrentUser(
                userId,
                principal.FindFirstValue(ClaimTypes.Email)
                    ?? principal.FindFirstValue("email")
                    ?? string.Empty,
                membership.OrganizationId,
                membership.Role,
                permissions.For(membership.Role)));

            await _next(context);
        }

        private static Guid ReadUserId(ClaimsPrincipal principal) {
            var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub");

            return Guid.TryParse(subject, out var userId)
                ? userId
                : throw new UnauthenticatedException(
                    "The access token does not contain a usable subject claim.");
        }

        private static OrganizationMembership? ReadMembershipClaims(ClaimsPrincipal principal) {
            var organizationId = principal.FindFirstValue(OrganizationIdClaim);
            var role = principal.FindFirstValue(OrganizationRoleClaim);

            return Guid.TryParse(organizationId, out var parsedId)
                && Enum.TryParse<OrganizationRole>(role, ignoreCase: true, out var parsedRole)
                    ? new OrganizationMembership(parsedId, parsedRole)
                    : null;
        }
    }
}
