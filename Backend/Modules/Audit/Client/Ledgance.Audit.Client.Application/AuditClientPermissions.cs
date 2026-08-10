using Ledgance.Shared.Application.Identity;

namespace Ledgance.Audit.Client.Application {
    public static class AuditClientPermissions {
        public const string Read = "audit:clients:read";
        public const string Manage = "audit:clients:manage";

        public static PermissionRegistry RegisterInto(PermissionRegistry registry) =>
            registry
                .GrantFrom(Read, OrganizationRole.Viewer)
                .GrantFrom(Manage, OrganizationRole.Manager);
    }
}
