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
        public string ContactName { get; set; } = default!;
        public string? Website { get; set; }
        public bool IsArchived { get; set; }
        public int ActiveEngagements { get; set; }
        public int TotalEngagements { get; set; }
    }

    public class GetPaginatedClientsQueryHandler
        : IRequestHandler<GetPaginatedClientsQuery, PaginatedResult<GetPaginatedClientsQueryRow>> {
        private readonly IClientRepository _clients;
        private readonly IClientEngagementCounter _engagements;

        public GetPaginatedClientsQueryHandler(IClientRepository clients,
            IClientEngagementCounter engagements) {
            _clients = clients;
            _engagements = engagements;
        }

        public async Task<PaginatedResult<GetPaginatedClientsQueryRow>> HandleAsync(
            GetPaginatedClientsQuery request, CancellationToken ct) {
            var page = Math.Max(1, request.Page);
            var pageSize = Math.Clamp(request.PageSize, 1, 100);

            var result = await _clients.ListPageAsync(page, pageSize, request.SearchValue, ct);

            var counts = await _engagements.CountForClientsAsync(
                result.Rows.Select(client => client.Id), ct);

            var rows = result.Rows
                .Select(client => {
                    var engagements = counts.GetValueOrDefault(client.Id,
                        new ClientEngagementCounts(0, 0));

                    return new GetPaginatedClientsQueryRow {
                        Id = client.Id,
                        Name = client.Name,
                        Email = client.ContactEmail,
                        Phone = client.ContactPhone,
                        Industry = client.Industry,
                        ContactName = client.ContactName,
                        Website = client.Website,
                        IsArchived = client.IsArchived,
                        ActiveEngagements = engagements.Active,
                        TotalEngagements = engagements.Total
                    };
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
