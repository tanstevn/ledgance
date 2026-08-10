using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Shared.Application.Onboarding {
    [AllowWithoutOrganization]
    public class ProvisionOrganizationCommand : ICommand<Result<ProvisionOrganizationCommandResult>> {
        public string OrganizationName { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
    }

    public class ProvisionOrganizationCommandResult {
        public Guid OrganizationId { get; set; }
    }

    public class ProvisionOrganizationCommandValidator : AbstractValidator<ProvisionOrganizationCommand> {
        public ProvisionOrganizationCommandValidator() {
            RuleFor(x => x.OrganizationName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.DisplayName).MaximumLength(100);
        }
    }
}
