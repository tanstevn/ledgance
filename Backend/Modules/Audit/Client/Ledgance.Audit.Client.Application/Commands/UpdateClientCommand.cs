using FluentValidation;
using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Commands {
    [RequiresPermission(AuditClientPermissions.Manage)]
    public class UpdateClientCommand : ICommand<Result<bool>> {
        public Guid Id { get; set; }
        public ClientInfoRecord ClientInfo { get; set; } = default!;
    }

    public class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand> {
        public UpdateClientCommandValidator() {
            RuleFor(x => x.Id).NotEmpty();
            RuleFor(x => x.ClientInfo).NotNull();

            When(x => x.ClientInfo is not null, () => {
                RuleFor(x => x.ClientInfo.Name).NotEmpty().MaximumLength(100);
                RuleFor(x => x.ClientInfo.Email).NotEmpty().EmailAddress().MaximumLength(100);
                RuleFor(x => x.ClientInfo.Phone).NotEmpty().MaximumLength(20);
                RuleFor(x => x.ClientInfo.Industry).NotEmpty().MaximumLength(50);
            });
        }
    }

    public class UpdateClientCommandHandler : IRequestHandler<UpdateClientCommand, Result<bool>> {
        private readonly IClientRepository _clients;
        private readonly IActivityRecorder _activity;

        public UpdateClientCommandHandler(IClientRepository clients, IActivityRecorder activity) {
            _clients = clients;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateClientCommand request, CancellationToken ct) {
            var client = await _clients.FindAsync(request.Id, ct);

            if (client is null) {
                return Result<bool>.Error("Client was not found.");
            }

            var info = request.ClientInfo;
            client.Update(info.Name, info.Industry, info.ContactName, info.Email,
                info.Phone, info.Website, info.Address);

            await _clients.UpdateAsync(client, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "client.updated",
                "Client", client.Id, $"Client '{client.Name}' was updated."), ct);

            return Result<bool>.Success(true);
        }
    }
}
