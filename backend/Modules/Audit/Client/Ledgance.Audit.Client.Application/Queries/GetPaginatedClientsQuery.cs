using Ledgance.Audit.Client.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Queries {
    [RequiresPermission(AuditClientPermissions.Read)]
    public class GetPaginatedClientsQuery : PaginatedRequest<GetPaginatedClientsQueryRow> { }

    public class GetPaginatedClientsQueryRow {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public string Industry { get; set; } = default!;
        public bool IsArchived { get; set; }
    }

    public class GetPaginatedClientsQueryHandler
        : IRequestHandler<GetPaginatedClientsQuery, PaginatedResult<GetPaginatedClientsQueryRow>> {
        private readonly IClientRepository _clients;

        public GetPaginatedClientsQueryHandler(IClientRepository clients) {
            _clients = clients;
        }

        public async Task<PaginatedResult<GetPaginatedClientsQueryRow>> HandleAsync(
            GetPaginatedClientsQuery request, CancellationToken ct) {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var result = await _clients.ListPageAsync(page, pageSize, request.SearchValue, ct);

            var rows = result.Rows
                .Select(client => new GetPaginatedClientsQueryRow {
                    Id = client.Id,
                    Name = client.Name,
                    Email = client.ContactEmail,
                    Phone = client.ContactPhone,
                    Industry = client.Industry,
                    IsArchived = client.IsArchived
                })
                .ToList();

            return new PaginatedResult<GetPaginatedClientsQueryRow> {
                Successful = true,
                Data = rows,
                PageNumber = page,
                ItemsPerPage = pageSize,
                ResultsCount = rows.Count,
                TotalResultsCount = (int)result.TotalCount,
                TotalPages = (int)Math.Ceiling(result.TotalCount / (decimal)pageSize)
            };
        }
    }
}
