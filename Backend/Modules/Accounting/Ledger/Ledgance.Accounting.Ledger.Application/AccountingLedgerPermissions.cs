using Ledgance.Shared.Application.Identity;

namespace Ledgance.Accounting.Ledger.Application {
    public static class AccountingLedgerPermissions {
        public const string Read = "accounting:ledger:read";
        public const string Contribute = "accounting:ledger:contribute";
        public const string Manage = "accounting:ledger:manage";

        public static PermissionRegistry RegisterInto(PermissionRegistry registry) =>
            registry
                .GrantFrom(Read, OrganizationRole.Viewer)
                .GrantFrom(Contribute, OrganizationRole.Member)
                .GrantFrom(Manage, OrganizationRole.Manager);
    }

    public class MediatorAnchor { }
}
