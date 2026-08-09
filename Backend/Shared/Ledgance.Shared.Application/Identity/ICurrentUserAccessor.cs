using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Shared.Application.Identity {
    /// <summary>
    /// Resolved once per request before the mediator runs, so every downstream consumer
    /// reads the same organization context without re-querying membership.
    /// </summary>
    public interface ICurrentUserAccessor {
        CurrentUser? Current { get; }
    }

    public interface ICurrentUserInitializer {
        void Set(CurrentUser user);
    }

    public static class CurrentUserAccessorExtensions {
        public static CurrentUser Require(this ICurrentUserAccessor accessor) =>
            accessor.Current ?? throw new UnauthenticatedException();

        public static Guid RequireOrganizationId(this ICurrentUserAccessor accessor) =>
            accessor.Require().OrganizationId;
    }
}
