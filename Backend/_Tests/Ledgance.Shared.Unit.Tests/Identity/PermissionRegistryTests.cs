using Ledgance.Shared.Application.Identity;

namespace Ledgance.Shared.Unit.Tests.Identity {
    public class PermissionRegistryTests {
        [Fact]
        public void GrantFrom_grants_to_the_minimum_role_and_every_role_above_it() {
            var registry = new PermissionRegistry()
                .GrantFrom("audit:engagement:approve", OrganizationRole.Manager);

            Assert.Contains("audit:engagement:approve", registry.For(OrganizationRole.Manager));
            Assert.Contains("audit:engagement:approve", registry.For(OrganizationRole.Admin));
            Assert.Contains("audit:engagement:approve", registry.For(OrganizationRole.Owner));
        }

        [Fact]
        public void GrantFrom_does_not_grant_to_roles_below_the_minimum() {
            var registry = new PermissionRegistry()
                .GrantFrom("audit:engagement:approve", OrganizationRole.Manager);

            Assert.DoesNotContain("audit:engagement:approve", registry.For(OrganizationRole.Member));
            Assert.DoesNotContain("audit:engagement:approve", registry.For(OrganizationRole.Viewer));
        }

        [Fact]
        public void For_returns_an_empty_set_for_a_role_with_no_grants() {
            Assert.Empty(new PermissionRegistry().For(OrganizationRole.Owner));
        }

        [Fact]
        public void Shared_permissions_keep_billing_management_with_the_owner() {
            var registry = SharedPermissions.RegisterInto(new PermissionRegistry());

            Assert.Contains(SharedPermissions.BillingManage, registry.For(OrganizationRole.Owner));
            Assert.DoesNotContain(SharedPermissions.BillingManage, registry.For(OrganizationRole.Admin));
            Assert.Contains(SharedPermissions.BillingRead, registry.For(OrganizationRole.Admin));
        }
    }
}
