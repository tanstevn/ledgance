using FluentValidation;
using Ledgance.Audit.Client.Application.Commands;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.TestInfrastructure;
using CreateClientResult = Ledgance.Shared.Application.Models.Result<
    Ledgance.Audit.Client.Application.Commands.CreateClientCommandResult>;

namespace Ledgance.Audit.Unit.Tests.Clients {
    public class CreateClientCommandTests {
        private static CreateClientCommand Valid() =>
            new() {
                ClientInfo = new ClientInfoRecord("Northgate Holdings",
                    "finance@northgate.test", "+63 2 8555 0100", "Manufacturing")
            };

        private static MediatorTestHarness Harness(bool authenticated) =>
            new MediatorTestHarness(authenticated ? TestIdentity.User() : null)
                .WithHandler<CreateClientCommand, CreateClientResult, CreateClientCommandHandler>()
                .WithValidator<CreateClientCommand>(new CreateClientCommandValidator());

        [Fact]
        public async Task An_unauthenticated_caller_cannot_create_a_client() {
            await Assert.ThrowsAsync<UnauthenticatedException>(
                () => Harness(authenticated: false).SendAsync(Valid()));
        }

        [Fact]
        public async Task An_invalid_client_is_rejected_by_the_validation_pipeline() {
            var command = Valid();
            command.ClientInfo = command.ClientInfo with { Email = "not-an-email" };

            await Assert.ThrowsAsync<ValidationException>(
                () => Harness(authenticated: true).SendAsync(command));
        }

        [Fact]
        public async Task A_valid_client_reaches_the_handler() {
            var result = await Harness(authenticated: true).SendAsync(Valid());

            Assert.True(result.Successful);
        }
    }
}
