using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Infrastructure.Supabase;
using Ledgance.TestInfrastructure;

namespace Ledgance.Shared.Unit.Tests.Supabase {
    public class TenantScopeTests {
        private sealed class TenantRow : IEntityModel, IOrganizationOwned {
            public Guid Id { get; set; }
            public Guid OrganizationId { get; set; }
        }

        private sealed class SharedRow : IEntityModel {
            public Guid Id { get; set; }
        }

        [Fact]
        public void Stamp_forces_the_callers_organization_onto_new_rows() {
            var row = new TenantRow { OrganizationId = TestIdentity.OtherOrganizationId };

            TenantScope.Stamp(row, TestIdentity.DefaultOrganizationId);

            Assert.Equal(TestIdentity.DefaultOrganizationId, row.OrganizationId);
        }

        [Fact]
        public void Guard_rejects_a_row_belonging_to_another_organization() {
            var row = new TenantRow { OrganizationId = TestIdentity.OtherOrganizationId };

            Assert.Throws<ForbiddenException>(
                () => TenantScope.Guard(row, TestIdentity.DefaultOrganizationId));
        }

        [Fact]
        public void Guard_accepts_a_row_from_the_callers_organization() {
            var row = new TenantRow { OrganizationId = TestIdentity.DefaultOrganizationId };

            TenantScope.Guard(row, TestIdentity.DefaultOrganizationId);
        }

        [Fact]
        public void Rows_that_are_not_tenant_owned_are_left_alone() {
            var row = new SharedRow();

            TenantScope.Stamp(row, TestIdentity.DefaultOrganizationId);
            TenantScope.Guard(row, TestIdentity.DefaultOrganizationId);
        }
    }
}
