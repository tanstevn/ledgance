using Ledgance.Shared.Application.Identity;

namespace Ledgance.Audit.Engagement.Application {
    public static class AuditEngagementPermissions {
        public const string Read = "audit:engagements:read";
        public const string Manage = "audit:engagements:manage";
        public const string Contribute = "audit:engagements:contribute";
        public const string Approve = "audit:engagements:approve";

        public static PermissionRegistry RegisterInto(PermissionRegistry registry) =>
            registry
                .GrantFrom(Read, OrganizationRole.Viewer)
                .GrantFrom(Contribute, OrganizationRole.Member)
                .GrantFrom(Manage, OrganizationRole.Manager)
                .GrantFrom(Approve, OrganizationRole.Manager);
    }

    public class MediatorAnchor { }
}
