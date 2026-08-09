using FluentValidation;
using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Queries {
    [RequiresPermission(AuditClientPermissions.Read)]
    public class GetClientInfoByIdQuery : IQuery<Result<GetClientInfoByIdQueryResult>> {
        public Guid Id { get; set; }
    }

    public class GetClientInfoByIdQueryResult {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public string Industry { get; set; } = default!;
        public string ContactName { get; set; } = default!;
        public string? Website { get; set; }
        public string? Address { get; set; }
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetClientInfoByIdQueryValidator : AbstractValidator<GetClientInfoByIdQuery> {
        public GetClientInfoByIdQueryValidator() {
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public class GetClientInfoByIdQueryHandler
        : IRequestHandler<GetClientInfoByIdQuery, Result<GetClientInfoByIdQueryResult>> {
        private readonly IClientRepository _clients;

        public GetClientInfoByIdQueryHandler(IClientRepository clients) {
            _clients = clients;
        }

        public async Task<Result<GetClientInfoByIdQueryResult>> HandleAsync(
            GetClientInfoByIdQuery request, CancellationToken ct) {
            var client = await _clients.FindAsync(request.Id, ct);

            if (client is null) {
                return Result<GetClientInfoByIdQueryResult>.Error("Client was not found.");
            }

            return Result<GetClientInfoByIdQueryResult>.Success(new GetClientInfoByIdQueryResult {
                Id = client.Id,
                Name = client.Name,
                Email = client.ContactEmail,
                Phone = client.ContactPhone,
                Industry = client.Industry,
                ContactName = client.ContactName,
                Website = client.Website,
                Address = client.Address,
                IsArchived = client.IsArchived,
                CreatedAt = client.CreatedAt
            });
        }
    }
}
