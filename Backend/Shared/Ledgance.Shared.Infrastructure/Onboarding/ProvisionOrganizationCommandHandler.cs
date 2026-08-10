using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Onboarding;

namespace Ledgance.Shared.Infrastructure.Onboarding {
    public class ProvisionOrganizationCommandHandler
        : IRequestHandler<ProvisionOrganizationCommand, Result<ProvisionOrganizationCommandResult>> {
        private readonly IAuthenticatedPrincipalAccessor _principal;
        private readonly IOrganizationDirectory _directory;

        public ProvisionOrganizationCommandHandler(IAuthenticatedPrincipalAccessor principal,
            IOrganizationDirectory directory) {
            _principal = principal;
            _directory = directory;
        }

        public async Task<Result<ProvisionOrganizationCommandResult>> HandleAsync(
            ProvisionOrganizationCommand request, CancellationToken ct) {
            var principal = _principal.RequirePrincipal();

            if (await _directory.HasAnyMembershipAsync(principal.UserId, ct)) {
                return Result<ProvisionOrganizationCommandResult>
                    .Error("This account already belongs to an organization.");
            }

            var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? principal.Email.Split('@')[0]
                : request.DisplayName.Trim();

            var organizationId = await _directory.CreateOrganizationWithOwnerAsync(
                request.OrganizationName.Trim(), principal.UserId, displayName,
                principal.Email, ct);

            return Result<ProvisionOrganizationCommandResult>
                .Success(new ProvisionOrganizationCommandResult { OrganizationId = organizationId });
        }
    }
}
