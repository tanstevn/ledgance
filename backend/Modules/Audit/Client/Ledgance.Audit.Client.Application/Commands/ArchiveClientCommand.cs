using FluentValidation;
using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Commands {
    [RequiresPermission(AuditClientPermissions.Manage)]
    public class ArchiveClientCommand : ICommand<Result<bool>> {
        public Guid Id { get; set; }
    }

    public class ArchiveClientCommandValidator : AbstractValidator<ArchiveClientCommand> {
        public ArchiveClientCommandValidator() {
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public class ArchiveClientCommandHandler : IRequestHandler<ArchiveClientCommand, Result<bool>> {
        private readonly IClientRepository _clients;
        private readonly IClientEngagementCounter _engagements;
        private readonly IActivityRecorder _activity;

        public ArchiveClientCommandHandler(IClientRepository clients,
            IClientEngagementCounter engagements, IActivityRecorder activity) {
            _clients = clients;
            _engagements = engagements;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(ArchiveClientCommand request, CancellationToken ct) {
            var client = await _clients.FindAsync(request.Id, ct);

            if (client is null) {
                return Result<bool>.Error("Client was not found.");
            }

            var activeEngagements = await _engagements.CountActiveEngagementsAsync(client.Id, ct);
            client.Archive(activeEngagements > 0);

            await _clients.UpdateAsync(client, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "client.archived",
                "Client", client.Id, $"Client '{client.Name}' was archived."), ct);

            return Result<bool>.Success(true);
        }
    }
}
