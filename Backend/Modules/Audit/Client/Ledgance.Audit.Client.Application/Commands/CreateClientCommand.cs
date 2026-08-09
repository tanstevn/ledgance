using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Client.Application.Commands {
    public record ClientInfoRecord(string Name, string Email, string Phone, string Industry);

    public class CreateClientCommand : ICommand<Result<CreateClientCommandResult>> {
        public ClientInfoRecord ClientInfo { get; set; } = default!;
    }

    public class CreateClientCommandResult {
        public long Id { get; set; }
    }

    public class CreateClientCommandValidator : AbstractValidator<CreateClientCommand> {
        public CreateClientCommandValidator() {
            // Implement the right rules to apply here
            // Adjust or change these rules on what is the right approach
            RuleFor(x => x.ClientInfo).NotNull();
            RuleFor(x => x.ClientInfo.Name).NotEmpty().MaximumLength(100);
            RuleFor(x => x.ClientInfo.Email).NotEmpty().EmailAddress().MaximumLength(100);
            RuleFor(x => x.ClientInfo.Phone).NotEmpty().MaximumLength(20);
            RuleFor(x => x.ClientInfo.Industry).NotEmpty().MaximumLength(50);
        }
    }

    public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, Result<CreateClientCommandResult>> {
        public Task<Result<CreateClientCommandResult>> HandleAsync(CreateClientCommand request, CancellationToken ct) {
            // Implementation here and
            // right value to pass as argument for Result<T>.Success

            return Task.FromResult(Result<CreateClientCommandResult>
                .Success(new CreateClientCommandResult { Id = 1 }));
        }
    }
}
