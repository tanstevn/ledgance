using Ledgance.Audit.Client.Application.Queries;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Engagements;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.TestInfrastructure;
using ClientPorts = Ledgance.Audit.Client.Application.Ports;
using DomainClient = Ledgance.Audit.Client.Domain.AuditClient;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    /// <summary>
    /// The paged list queries behind the clients grid and the engagements page: the UI shows
    /// numbered pages and filters, so the server must do the paging and the filtering.
    /// </summary>
    public class AuditListWorkflowTests {
        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly StubClientLookup _clients = new();
        private readonly InMemoryClientRepository _clientRepository = new();
        private readonly InMemoryClientEngagementCounter _counter = new();

        private MediatorTestHarness EngagementHarness() =>
            new MediatorTestHarness(TestIdentity.User(OrganizationRole.Manager,
                    permissions: [AuditEngagementPermissions.Read]))
                .WithHandler<GetPaginatedEngagementsQuery, PaginatedResult<EngagementListRow>,
                    GetPaginatedEngagementsQueryHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<IClientLookup>(_clients);

        private MediatorTestHarness ClientHarness() =>
            new MediatorTestHarness(TestIdentity.User(OrganizationRole.Viewer,
                    permissions: [Ledgance.Audit.Client.Application.AuditClientPermissions.Read]))
                .WithHandler<GetPaginatedClientsQuery,
                    PaginatedResult<GetPaginatedClientsQueryRow>,
                    GetPaginatedClientsQueryHandler>()
                .WithService<ClientPorts.IClientRepository>(_clientRepository)
                .WithService<ClientPorts.IClientEngagementCounter>(_counter);

        private DomainEngagement AddEngagement(Guid clientId, string name,
            EngagementStatus status) {
            var engagement = DomainEngagement.Create(clientId, name,
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());

            if (status is not EngagementStatus.Planning) {
                engagement = DomainEngagement.Restore(engagement.Id, engagement.ClientId,
                    engagement.Name, engagement.Type, status, engagement.PeriodStart,
                    engagement.PeriodEnd, engagement.FiscalYearEnd, engagement.BudgetHours,
                    engagement.CreatedBy, engagement.CreatedAt, null, null);
            }

            _engagements.Engagements.Add(engagement);
            return engagement;
        }

        [Fact]
        public async Task The_engagement_page_returns_ten_records_and_the_page_count() {
            var clientId = Guid.NewGuid();

            foreach (var index in Enumerable.Range(1, 23)) {
                AddEngagement(clientId, $"FY20{index:00} Audit", EngagementStatus.Planning);
            }

            var harness = EngagementHarness();

            var first = await harness.SendAsync(
                new GetPaginatedEngagementsQuery { Page = 1, PageSize = 10 });

            Assert.True(first.Successful);
            Assert.Equal(10, first.Data!.Count());
            Assert.Equal(23, first.TotalResultsCount);
            Assert.Equal(3, first.TotalPages);

            var last = await harness.SendAsync(
                new GetPaginatedEngagementsQuery { Page = 3, PageSize = 10 });

            Assert.Equal(3, last.Data!.Count());
            Assert.Equal(3, last.PageNumber);
        }

        [Fact]
        public async Task The_engagement_page_filters_by_status_and_by_client() {
            var acme = Guid.NewGuid();
            var globex = Guid.NewGuid();

            AddEngagement(acme, "Acme fieldwork", EngagementStatus.Fieldwork);
            AddEngagement(acme, "Acme planning", EngagementStatus.Planning);
            AddEngagement(globex, "Globex fieldwork", EngagementStatus.Fieldwork);

            var harness = EngagementHarness();

            var byStatus = await harness.SendAsync(new GetPaginatedEngagementsQuery {
                Status = EngagementStatus.Fieldwork
            });

            Assert.Equal(2, byStatus.TotalResultsCount);
            Assert.All(byStatus.Data!, row => Assert.Equal("Fieldwork", row.Status));

            var byClient = await harness.SendAsync(new GetPaginatedEngagementsQuery {
                ClientId = acme
            });

            Assert.Equal(2, byClient.TotalResultsCount);
            Assert.All(byClient.Data!, row => Assert.Equal(acme, row.ClientId));

            var both = await harness.SendAsync(new GetPaginatedEngagementsQuery {
                ClientId = acme,
                Status = EngagementStatus.Fieldwork
            });

            var only = Assert.Single(both.Data!);
            Assert.Equal("Acme fieldwork", only.Name);
        }

        [Fact]
        public async Task The_client_page_carries_the_engagement_counts_each_card_shows() {
            var client = DomainClient.Restore(Guid.NewGuid(), "Halcyon Manufacturing",
                "Industrial Manufacturing", "Priya Nair", "priya.nair@halcyon.test",
                "+1 415 555 0142", "halcyon.test", null, false, DateTime.UtcNow);

            _clientRepository.Clients.Add(client);
            _counter.Counts[client.Id] = new ClientPorts.ClientEngagementCounts(2, 5);

            var page = await ClientHarness().SendAsync(
                new GetPaginatedClientsQuery { Page = 1, PageSize = 10 });

            var row = Assert.Single(page.Data!);
            Assert.Equal(2, row.ActiveEngagements);
            Assert.Equal(5, row.TotalEngagements);
            Assert.Equal("Priya Nair", row.ContactName);
            Assert.Equal("halcyon.test", row.Website);
        }

        [Fact]
        public async Task A_client_with_no_engagements_reports_zero_rather_than_failing() {
            _clientRepository.Clients.Add(DomainClient.Restore(Guid.NewGuid(), "Cedar & Ash",
                "Retail", "Tomas Ruiz", "tomas@cedarash.test", "+1 206 555 0177", null, null,
                false, DateTime.UtcNow));

            var page = await ClientHarness().SendAsync(new GetPaginatedClientsQuery());

            var row = Assert.Single(page.Data!);
            Assert.Equal(0, row.ActiveEngagements);
            Assert.Equal(0, row.TotalEngagements);
        }
    }
}
