using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Queries {
    public class GetClientInfoByIdQuery : IQuery<Result<GetClientInfoByIdQueryResult>> {
        public long Id { get; set; }
    }

    public class GetClientInfoByIdQueryResult {
        public long Id { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Phone { get; set; } = default!;
        public string Industry { get; set; } = default!;
    }

    public class GetClientInfoByIdQueryValidator : AbstractValidator<GetClientInfoByIdQuery> {
        public GetClientInfoByIdQueryValidator() {
            // Implement the right rules to apply here
            // Adjust or change these rules on what is the right approach
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }

    public class GetClientInfoByIdQueryHandler : IRequestHandler<GetClientInfoByIdQuery, Result<GetClientInfoByIdQueryResult>> {
        public Task<Result<GetClientInfoByIdQueryResult>> HandleAsync(GetClientInfoByIdQuery request, CancellationToken ct) {
            // Implementation here and
            // right value to pass as argument for Result<T>.Success
            return Task.FromResult(Result<GetClientInfoByIdQueryResult>
                .Success(new GetClientInfoByIdQueryResult {
                    Id = request.Id,
                    Name = "Sample Name",
                    Email = "sample@example.com",
                    Phone = "123-456-7890",
                    Industry = "Sample Industry"
                }));
        }
    }
}
