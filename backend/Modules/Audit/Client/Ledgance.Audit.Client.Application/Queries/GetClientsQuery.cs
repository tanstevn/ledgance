using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Queries {
    [RequiresPermission(AuditClientPermissions.Read)]
    public class GetClientsQuery : IQuery<Result<IEnumerable<GetClientsQueryResult>>> {
        public bool IncludeArchived { get; set; }
    }

    public class GetClientsQueryResult {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public string Industry { get; set; } = default!;
        public string ContactName { get; set; } = default!;
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetClientsQueryHandler
        : IRequestHandler<GetClientsQuery, Result<IEnumerable<GetClientsQueryResult>>> {
        private readonly IClientRepository _clients;

        public GetClientsQueryHandler(IClientRepository clients) {
            _clients = clients;
        }

        public async Task<Result<IEnumerable<GetClientsQueryResult>>> HandleAsync(
            GetClientsQuery request, CancellationToken ct) {
            var clients = await _clients.ListAsync(request.IncludeArchived, ct);

            return Result<IEnumerable<GetClientsQueryResult>>.Success(clients
                .Select(client => new GetClientsQueryResult {
                    Id = client.Id,
                    Name = client.Name,
                    Email = client.ContactEmail,
                    Phone = client.ContactPhone,
                    Industry = client.Industry,
                    ContactName = client.ContactName,
                    IsArchived = client.IsArchived,
                    CreatedAt = client.CreatedAt
                }));
        }
    }
}
