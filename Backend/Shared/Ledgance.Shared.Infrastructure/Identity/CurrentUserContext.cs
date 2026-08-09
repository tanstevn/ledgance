using Ledgance.Shared.Application.Identity;

namespace Ledgance.Shared.Infrastructure.Identity {
    internal sealed class CurrentUserContext : ICurrentUserAccessor, ICurrentUserInitializer {
        public CurrentUser? Current { get; private set; }

        public void Set(CurrentUser user) {
            Current = user;
        }
    }
}
