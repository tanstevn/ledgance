using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Shared.Application.Identity {
    public sealed record AuthenticatedPrincipal(Guid UserId, string Email);

    /// <summary>
    /// The verified token identity, available even before the user belongs to an organization.
    /// Onboarding is the only flow that should stop here.
    /// </summary>
    public interface IAuthenticatedPrincipalAccessor {
        AuthenticatedPrincipal? Principal { get; }
    }

    /// <summary>
    /// Resolved once per request before the mediator runs, so every downstream consumer
    /// reads the same organization context without re-querying membership.
    /// </summary>
    public interface ICurrentUserAccessor : IAuthenticatedPrincipalAccessor {
        CurrentUser? Current { get; }
    }

    public interface ICurrentUserInitializer {
        void SetPrincipal(AuthenticatedPrincipal principal);
        void Set(CurrentUser user);
    }

    public static class CurrentUserAccessorExtensions {
        public static CurrentUser Require(this ICurrentUserAccessor accessor) =>
            accessor.Current ?? throw (accessor.Principal is null
                ? new UnauthenticatedException()
                : (Exception)new ForbiddenException(
                    "This account is not a member of any organization."));

        public static Guid RequireOrganizationId(this ICurrentUserAccessor accessor) =>
            accessor.Require().OrganizationId;

        public static AuthenticatedPrincipal RequirePrincipal(
            this IAuthenticatedPrincipalAccessor accessor) =>
            accessor.Principal ?? throw new UnauthenticatedException();
    }
}
