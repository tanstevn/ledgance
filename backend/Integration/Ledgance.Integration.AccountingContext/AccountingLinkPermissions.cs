using Ledgance.Shared.Application.Identity;

namespace Ledgance.Integration.AccountingContext {
    public static class AccountingLinkPermissions {
        public const string Read = "integration:accounting_link:read";
        public const string Manage = "integration:accounting_link:manage";

        public static PermissionRegistry RegisterInto(PermissionRegistry registry) =>
            registry
                .GrantFrom(Read, OrganizationRole.Viewer)
                .GrantFrom(Manage, OrganizationRole.Admin);
    }

    public class MediatorAnchor { }
}
