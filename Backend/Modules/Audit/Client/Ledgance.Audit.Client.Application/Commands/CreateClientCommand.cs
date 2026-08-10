using FluentValidation;
using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using DomainClient = Ledgance.Audit.Client.Domain.AuditClient;

namespace Ledgance.Audit.Client.Application.Commands {
    public record ClientInfoRecord(string Name, string Email, string Phone, string Industry,
        string ContactName = "", string? Website = null, string? Address = null);

    [RequiresPermission(AuditClientPermissions.Manage)]
    public class CreateClientCommand : ICommand<Result<CreateClientCommandResult>> {
        public ClientInfoRecord ClientInfo { get; set; } = default!;
    }

    public class CreateClientCommandResult {
        public Guid Id { get; set; }
    }

    public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand> {
        public CreateClientCommandValidator() {
            RuleFor(x => x.ClientInfo).NotNull();

            When(x => x.ClientInfo is not null, () => {
                RuleFor(x => x.ClientInfo.Name).NotEmpty().MaximumLength(100);
                RuleFor(x => x.ClientInfo.Email).NotEmpty().EmailAddress().MaximumLength(100);
                RuleFor(x => x.ClientInfo.Phone).NotEmpty().MaximumLength(20);
                RuleFor(x => x.ClientInfo.Industry).NotEmpty().MaximumLength(50);
                RuleFor(x => x.ClientInfo.ContactName).MaximumLength(100);
                RuleFor(x => x.ClientInfo.Website).MaximumLength(200);
                RuleFor(x => x.ClientInfo.Address).MaximumLength(300);
            });
        }
    }

    public class CreateClientCommandHandler
        : IRequestHandler<CreateClientCommand, Result<CreateClientCommandResult>> {
        private readonly IClientRepository _clients;
        private readonly IEntitlementService _entitlements;
        private readonly IActivityRecorder _activity;
        private readonly ICurrentUserAccessor _currentUser;

        public CreateClientCommandHandler(IClientRepository clients,
            IEntitlementService entitlements, IActivityRecorder activity,
            ICurrentUserAccessor currentUser) {
            _clients = clients;
            _entitlements = entitlements;
            _activity = activity;
            _currentUser = currentUser;
        }

        public async Task<Result<CreateClientCommandResult>> HandleAsync(
            CreateClientCommand request, CancellationToken ct) {
            var entitlements = await _entitlements.GetAsync(
                _currentUser.Require().OrganizationId, ProductModule.Audit, ct);

            var activeClients = await _clients.CountActiveAsync(ct);
            entitlements.RequireWithinLimit(Entitlements.MaxClients, activeClients + 1);

            var info = request.ClientInfo;
            var client = DomainClient.Create(info.Name, info.Industry, info.ContactName,
                info.Email, info.Phone, info.Website, info.Address);

            await _clients.AddAsync(client, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "client.created",
                "Client", client.Id, $"Client '{client.Name}' was created."), ct);

            return Result<CreateClientCommandResult>
                .Success(new CreateClientCommandResult { Id = client.Id });
        }
    }
}
