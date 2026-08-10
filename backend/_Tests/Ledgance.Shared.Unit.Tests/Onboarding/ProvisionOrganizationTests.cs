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

        public Task<bool> HasAnyMembershipAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(AlreadyMember);

        public Task<Guid> CreateOrganizationWithOwnerAsync(string organizationName,
            Guid ownerUserId, string ownerDisplayName, string ownerEmail,
            CancellationToken ct) {
            CreatedName = organizationName;
            return Task.FromResult(CreatedOrganizationId);
        }

        public Task<IReadOnlyList<OrganizationMemberInfo>> ListMembersAsync(
            Guid organizationId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<OrganizationMemberInfo>>([]);

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
