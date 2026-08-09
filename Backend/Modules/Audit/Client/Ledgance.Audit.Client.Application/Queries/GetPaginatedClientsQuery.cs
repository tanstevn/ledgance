using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Extensions;

namespace Ledgance.Audit.Client.Application.Queries {
    public class GetPaginatedClientsQuery : PaginatedRequest<GetPaginatedClientsQueryRow> { }

    public class GetPaginatedClientsQueryRow {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public string Industry { get; set; } = default!;
        public int TotalEngagements { get; set; }
    }

    public class GetPaginatedClientsQueryHandler : IRequestHandler<GetPaginatedClientsQuery, PaginatedResult<GetPaginatedClientsQueryRow>> {
        public async Task<PaginatedResult<GetPaginatedClientsQueryRow>> HandleAsync(GetPaginatedClientsQuery request, CancellationToken ct) {
            // Implementation here and change whatever is the
            // right value to pass as argument for PaginatedResult<T>.Success 
            var query = new List<object>()
                .Select(x => new GetPaginatedClientsQueryRow {
                    Id = 1,
                    Name = "Sample Name",
                    Email = "examplel@email.com",
                    Phone = "123-456-7890",
                    Industry = "Sample Industry",
                    TotalEngagements = 5
                })
                .AsQueryable();


            return await query.PaginateAsync(request);
        }
    }
}
