using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Queries {
    public class GetClientsQuery : IQuery<Result<IEnumerable<GetClientsQueryResult>>> { }

    public class GetClientsQueryResult {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public string Industry { get; set; } = default!;
        public int TotalEngagements { get; set; }
    }

    public class GetClientsQueryHandler : IRequestHandler<GetClientsQuery, Result<IEnumerable<GetClientsQueryResult>>> {
        public async Task<Result<IEnumerable<GetClientsQueryResult>>> HandleAsync(GetClientsQuery request, CancellationToken ct) {
            // Implementation here and
            // right value to pass as argument for Result<T>.Success 

            return Result<IEnumerable<GetClientsQueryResult>>
                .Success([]);
        }
    }
}
