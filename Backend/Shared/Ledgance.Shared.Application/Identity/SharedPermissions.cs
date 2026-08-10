namespace Ledgance.Shared.Application.Identity {
    public static class SharedPermissions {
        public const string OrganizationRead = "organization:read";
        public const string OrganizationManage = "organization:manage";
        public const string MembersRead = "organization:members:read";
        public const string MembersManage = "organization:members:manage";
        public const string BillingRead = "organization:billing:read";
        public const string BillingManage = "organization:billing:manage";

        public static PermissionRegistry RegisterInto(PermissionRegistry registry) =>
            registry
                .GrantFrom(OrganizationRead, OrganizationRole.Viewer)
                .GrantFrom(MembersRead, OrganizationRole.Member)
                .GrantFrom(BillingRead, OrganizationRole.Admin)
                .GrantFrom(MembersManage, OrganizationRole.Admin)
                .GrantFrom(OrganizationManage, OrganizationRole.Owner)
                .GrantFrom(BillingManage, OrganizationRole.Owner);
    }
}
