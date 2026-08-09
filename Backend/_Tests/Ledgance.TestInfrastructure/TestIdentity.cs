using Ledgance.Shared.Application.Identity;

namespace Ledgance.TestInfrastructure {
    public static class TestIdentity {
        public static readonly Guid DefaultOrganizationId =
            Guid.Parse("11111111-1111-1111-1111-111111111111");

        public static readonly Guid OtherOrganizationId =
            Guid.Parse("22222222-2222-2222-2222-222222222222");

        public static CurrentUser User(OrganizationRole role = OrganizationRole.Manager,
            Guid? organizationId = null, params string[] permissions) =>
            new(Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "member@ledgance.test",
                organizationId ?? DefaultOrganizationId,
                role,
                new HashSet<string>(permissions));

        public static CurrentUser UserWithRegisteredPermissions(
            OrganizationRole role = OrganizationRole.Manager,
            Guid? organizationId = null) {
            var registry = SharedPermissions.RegisterInto(new PermissionRegistry());

            return new CurrentUser(Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "member@ledgance.test",
                organizationId ?? DefaultOrganizationId,
                role,
                registry.For(role));
        }
    }

    public sealed class FakeCurrentUserAccessor : ICurrentUserAccessor, ICurrentUserInitializer {
        public FakeCurrentUserAccessor(CurrentUser? current = null) {
            Current = current;
        }

        public CurrentUser? Current { get; private set; }

        public void Set(CurrentUser user) {
            Current = user;
        }
    }
}
