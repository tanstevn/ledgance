using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Onboarding;
using Ledgance.Shared.Infrastructure.Onboarding;
using Ledgance.TestInfrastructure;

namespace Ledgance.Shared.Unit.Tests.Onboarding {
    internal sealed class StubDirectory : IOrganizationDirectory {
        public bool AlreadyMember { get; set; }
        public Guid CreatedOrganizationId { get; } = Guid.NewGuid();
        public string? CreatedName { get; private set; }
        public string? CreatedProduct { get; private set; }
        public List<string> EnabledProducts { get; } = [];

        public Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(AlreadyMember);

        public Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail, string? product,
            CancellationToken ct) {
            CreatedName = organizationName;
            CreatedProduct = product;
            return Task.FromResult(CreatedOrganizationId);
        }

        public Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(
            Guid organizationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrganizationMemberInfo>>([]);

        public Task<OrganizationInfo?> GetOrganizationAsync(Guid organizationId,
            CancellationToken ct) =>
            Task.FromResult<OrganizationInfo?>(CreatedName is null
                ? null
                : new OrganizationInfo(CreatedName,
                    CreatedProduct is null ? ["Audit", "Accounting"] : [CreatedProduct]));

        public Task AddProductAsync(Guid organizationId, string product,
            CancellationToken ct) {
            EnabledProducts.Add(product);
            return Task.CompletedTask;
        }

        public Task<OrganizationMemberInfo?> FindMemberAsync(Guid organizationId,
            Guid userId, CancellationToken ct) =>
            Task.FromResult<OrganizationMemberInfo?>(null);
    }

    public class ProvisionOrganizationTests {
        private static MediatorTestHarness Harness(StubDirectory directory,
            AuthenticatedPrincipal? principal) {
            var harness = new MediatorTestHarness(
                principal is null ? null : null);

            if (principal is not null) {
                harness.CurrentUser.SetPrincipal(principal);
            }

            return harness
                .WithHandler<ProvisionOrganizationCommand,
                    Result<ProvisionOrganizationCommandResult>,
                    ProvisionOrganizationCommandHandler>()
                .WithValidator<ProvisionOrganizationCommand>(
                    new ProvisionOrganizationCommandValidator())
                .WithService<IOrganizationDirectory>(directory);
        }

        [Fact]
        public async Task An_authenticated_user_without_an_organization_can_provision_one() {
            var directory = new StubDirectory();
            var harness = Harness(directory,
                new AuthenticatedPrincipal(Guid.NewGuid(), "new-user@ledgance.test"));

            var result = await harness.SendAsync(new ProvisionOrganizationCommand {
                OrganizationName = "Northgate Advisory"
            });

            Assert.True(result.Successful);
            Assert.Equal(directory.CreatedOrganizationId, result.Data!.OrganizationId);
            Assert.Equal("Northgate Advisory", directory.CreatedName);
        }

        [Fact]
        public async Task The_platform_chosen_at_signup_is_stored_with_the_organization() {
            var directory = new StubDirectory();
            var harness = Harness(directory,
                new AuthenticatedPrincipal(Guid.NewGuid(), "solo@ledgance.test"));

            var result = await harness.SendAsync(new ProvisionOrganizationCommand {
                OrganizationName = "Solo Books",
                Product = "Accounting"
            });

            Assert.True(result.Successful);
            Assert.Equal("Accounting", directory.CreatedProduct);
        }

        [Fact]
        public async Task An_unknown_product_is_rejected_by_validation() {
            var harness = Harness(new StubDirectory(),
                new AuthenticatedPrincipal(Guid.NewGuid(), "user@ledgance.test"));

            await Assert.ThrowsAsync<FluentValidation.ValidationException>(
                () => harness.SendAsync(new ProvisionOrganizationCommand {
                    OrganizationName = "Broken",
                    Product = "Payroll"
                }));
        }

        [Fact]
        public async Task Only_the_owner_may_enable_another_product() {
            var directory = new StubDirectory();

            MediatorTestHarness ProductHarness(Ledgance.Shared.Application.Identity.CurrentUser user) =>
                new MediatorTestHarness(user)
                    .WithHandler<EnableOrganizationProductCommand, Result<bool>,
                        EnableOrganizationProductCommandHandler>()
                    .WithValidator<EnableOrganizationProductCommand>(
                        new EnableOrganizationProductCommandValidator())
                    .WithService<IOrganizationDirectory>(directory);

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                ProductHarness(TestIdentity.User(OrganizationRole.Admin,
                        permissions: [SharedPermissions.MembersManage]))
                    .SendAsync(new EnableOrganizationProductCommand { Product = "Audit" }));

            Assert.Empty(directory.EnabledProducts);

            var result = await ProductHarness(TestIdentity.User(OrganizationRole.Owner,
                    permissions: [SharedPermissions.OrganizationManage]))
                .SendAsync(new EnableOrganizationProductCommand { Product = "Audit" });

            Assert.True(result.Successful);
            Assert.Equal(["Audit"], directory.EnabledProducts);
        }

        [Fact]
        public async Task An_unauthenticated_caller_cannot_provision() {
            var harness = Harness(new StubDirectory(), null);

            await Assert.ThrowsAsync<UnauthenticatedException>(
                () => harness.SendAsync(new ProvisionOrganizationCommand {
                    OrganizationName = "Northgate Advisory"
                }));
        }

        [Fact]
        public async Task A_user_who_already_belongs_to_an_organization_is_rejected() {
            var directory = new StubDirectory { AlreadyMember = true };
            var harness = Harness(directory,
                new AuthenticatedPrincipal(Guid.NewGuid(), "member@ledgance.test"));

            var result = await harness.SendAsync(new ProvisionOrganizationCommand {
                OrganizationName = "Second Org"
            });

            Assert.False(result.Successful);
            Assert.Null(directory.CreatedName);
        }
    }
}
