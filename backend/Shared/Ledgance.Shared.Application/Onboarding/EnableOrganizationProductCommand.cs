using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Shared.Application.Onboarding {
    /// <summary>
    /// Activates the other Ledgance product for an existing organization. This only widens
    /// what the dashboard shows — plan entitlements are unaffected, and a paid subscription
    /// for a module enables it regardless of this list.
    /// </summary>
    [RequiresPermission(SharedPermissions.OrganizationManage)]
    public class EnableOrganizationProductCommand : ICommand<Result<bool>> {
        public string Product { get; set; } = string.Empty;
    }

    public class EnableOrganizationProductCommandValidator
        : AbstractValidator<EnableOrganizationProductCommand> {
        public EnableOrganizationProductCommandValidator() {
            RuleFor(x => x.Product)
                .Must(product => product is "Audit" or "Accounting")
                .WithMessage("Product must be 'Audit' or 'Accounting'.");
        }
    }

    public class EnableOrganizationProductCommandHandler
        : IRequestHandler<EnableOrganizationProductCommand, Result<bool>> {
        private readonly IOrganizationDirectory _directory;
        private readonly ICurrentUserAccessor _currentUser;

        public EnableOrganizationProductCommandHandler(IOrganizationDirectory directory,
            ICurrentUserAccessor currentUser) {
            _directory = directory;
            _currentUser = currentUser;
        }

        public async Task<Result<bool>> HandleAsync(EnableOrganizationProductCommand request,
            CancellationToken ct) {
            await _directory.AddProductAsync(_currentUser.Require().OrganizationId,
                request.Product, ct);

            return Result<bool>.Success(true);
        }
    }
}
