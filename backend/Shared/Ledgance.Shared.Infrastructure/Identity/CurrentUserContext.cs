using Ledgance.Shared.Application.Identity;

namespace Ledgance.Shared.Infrastructure.Identity {
    internal sealed class CurrentUserContext : ICurrentUserAccessor, ICurrentUserInitializer {
        public AuthenticatedPrincipal? Principal { get; private set; }

        public CurrentUser? Current { get; private set; }

        public void SetPrincipal(AuthenticatedPrincipal principal) {
            Principal = principal;
        }

        public void Set(CurrentUser user) {
            Current = user;
            Principal ??= new AuthenticatedPrincipal(user.UserId, user.Email);
        }
    }
}
