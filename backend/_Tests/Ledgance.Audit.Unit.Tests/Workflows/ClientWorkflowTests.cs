using Ledgance.Audit.Client.Application.Commands;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;
using ClientPorts = Ledgance.Audit.Client.Application.Ports;
using CreateClientResult = Ledgance.Shared.Application.Models.Result<
    Ledgance.Audit.Client.Application.Commands.CreateClientCommandResult>;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    public class ClientWorkflowTests {
        private static CreateClientCommand ValidCommand(string name = "Northgate Holdings") =>
            new() {
                ClientInfo = new ClientInfoRecord(name, "finance@northgate.test",
                    "+63 2 8555 0100", "Manufacturing")
            };

        private static MediatorTestHarness Harness(InMemoryClientRepository repository,
            CurrentUser? user) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<CreateClientCommand, CreateClientResult, CreateClientCommandHandler>()
                .WithValidator<CreateClientCommand>(new CreateClientCommandValidator())
                .WithService<ClientPorts.IClientRepository>(repository)
                .WithService<IActivityRecorder>(new RecordingActivityRecorder());

            return harness;
        }

        [Fact]
        public async Task A_user_without_the_manage_permission_cannot_create_a_client() {
            var harness = Harness(new InMemoryClientRepository(),
                TestIdentity.User(OrganizationRole.Member));

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(ValidCommand()));
        }

        [Fact]
        public async Task A_manager_with_the_permission_creates_a_client() {
            var repository = new InMemoryClientRepository();
            var harness = Harness(repository, TestIdentity.User(OrganizationRole.Manager,
                permissions: Ledgance.Audit.Client.Application.AuditClientPermissions.Manage));

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);

            var result = await harness.SendAsync(ValidCommand());

            Assert.True(result.Successful);
            Assert.Single(repository.Clients);
        }

        [Fact]
        public async Task The_free_plan_client_limit_is_enforced() {
            var repository = new InMemoryClientRepository();
            var harness = Harness(repository, TestIdentity.User(OrganizationRole.Manager,
                permissions: Ledgance.Audit.Client.Application.AuditClientPermissions.Manage));

            harness.Entitlements.With(ProductModule.Audit, PlanCode.Free);

            var first = await harness.SendAsync(ValidCommand("First Client"));
            Assert.True(first.Successful);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(ValidCommand("Second Client")));

            Assert.Contains(Entitlements.MaxClients, exception.Message);
            Assert.Single(repository.Clients);
        }

        [Fact]
        public async Task Creating_a_client_writes_to_the_activity_trail() {
            var recorder = new RecordingActivityRecorder();
            var harness = new MediatorTestHarness(TestIdentity.User(OrganizationRole.Manager,
                    permissions: Ledgance.Audit.Client.Application.AuditClientPermissions.Manage))
                .WithHandler<CreateClientCommand, CreateClientResult, CreateClientCommandHandler>()
                .WithService<ClientPorts.IClientRepository>(new InMemoryClientRepository())
                .WithService<IActivityRecorder>(recorder);

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);

            await harness.SendAsync(ValidCommand());

            var entry = Assert.Single(recorder.Entries);
            Assert.Equal("client.created", entry.Action);
            Assert.Equal("Audit", entry.Module);

            // The summary is a predicate, so the reader renders "You created the client …" as
            // one sentence. A standalone sentence here would read "You Client 'X' was created."
            Assert.Equal("created the client Northgate Holdings.", entry.Summary);
            Assert.True(char.IsLower(entry.Summary[0]));
        }
    }
}
