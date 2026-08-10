using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.TestInfrastructure;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    public sealed class FakeLinkedAccountingSource : ILinkedAccountingSource {
        public LinkedAccountingAvailability Availability { get; set; } = new(true, null);
        public List<LinkedAccountingEntity> Entities { get; } = [];
        public LinkedTrialBalance? TrialBalance { get; set; }
        public Exception? Throws { get; set; }

        public Task<LinkedAccountingAvailability> GetAvailabilityAsync(CancellationToken ct) =>
            Task.FromResult(Availability);

        public Task<IReadOnlyList<LinkedAccountingEntity>> ListEntitiesAsync(
            CancellationToken ct) {
            if (Throws is not null) {
                throw Throws;
            }

            return Task.FromResult<IReadOnlyList<LinkedAccountingEntity>>(Entities);
        }

        public Task<LinkedTrialBalance?> GetTrialBalanceAsync(Guid accountingEntityId,
            Guid accountingPeriodId, CancellationToken ct) {
            if (Throws is not null) {
                throw Throws;
            }

            return Task.FromResult(TrialBalance);
        }
    }

    public class LinkedAccountingWorkflowTests {
        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryTrialBalanceRepository _trialBalances = new();
        private readonly FakeLinkedAccountingSource _linked = new();
        private readonly RecordingActivityRecorder _activity = new();

        private static CurrentUser Member() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Contribute]);

        private DomainEngagement SeedEngagement(Guid? teamUserId = null) {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());
            _engagements.Engagements.Add(engagement);

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id,
                    teamUserId.Value, EngagementRole.Senior));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<ImportTrialBalanceFromAccountingCommand,
                    Result<ImportTrialBalanceResult>,
                    ImportTrialBalanceFromAccountingCommandHandler>()
                .WithHandler<GetLinkedAccountingContextQuery,
                    Result<LinkedAccountingContextView>,
                    GetLinkedAccountingContextQueryHandler>()
                .WithService<ITrialBalanceRepository>(_trialBalances)
                .WithService<ILinkedAccountingSource>(_linked)
                .WithService<Ledgance.Shared.Application.Activity.IActivityRecorder>(_activity);

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            return harness;
        }

        private static LinkedTrialBalance BalancedTrialBalance() =>
            new("Acme", "March 2026", new DateOnly(2026, 3, 31), [
                new TrialBalanceLine("1010", "Cash", 750, 0),
                new TrialBalanceLine("4010", "Sales", 0, 750)
            ]);

        [Fact]
        public async Task A_non_team_member_cannot_import_from_accounting() {
            var engagement = SeedEngagement();
            _linked.TrialBalance = BalancedTrialBalance();

            await Assert.ThrowsAsync<ForbiddenException>(() => Harness(Member())
                .SendAsync(new ImportTrialBalanceFromAccountingCommand {
                    EngagementId = engagement.Id,
                    AccountingEntityId = Guid.NewGuid(),
                    AccountingPeriodId = Guid.NewGuid()
                }));

            Assert.Empty(_trialBalances.Imports);
        }

        [Fact]
        public async Task A_team_member_imports_a_provenance_stamped_trial_balance() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            _linked.TrialBalance = BalancedTrialBalance();

            var result = await Harness(user)
                .SendAsync(new ImportTrialBalanceFromAccountingCommand {
                    EngagementId = engagement.Id,
                    AccountingEntityId = Guid.NewGuid(),
                    AccountingPeriodId = Guid.NewGuid()
                });

            Assert.True(result.Successful);
            Assert.True(result.Data!.IsBalanced);
            Assert.Equal(2, result.Data.LineCount);

            var import = Assert.Single(_trialBalances.Imports);
            Assert.Equal(TrialBalanceSource.LedganceAccounting, import.Source);
            Assert.Contains("March 2026", import.PeriodLabel);

            var entry = Assert.Single(_activity.Entries);
            Assert.Equal("trial_balance.imported", entry.Action);
            Assert.Contains("Ledgance Accounting", entry.Summary);
            Assert.Contains("Acme", entry.Summary);
        }

        [Fact]
        public async Task A_missing_entitlement_surfaces_as_an_entitlement_failure() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            _linked.Throws = EntitlementException.NotIncluded("accounting_context_sharing");

            await Assert.ThrowsAsync<EntitlementException>(() => Harness(user)
                .SendAsync(new ImportTrialBalanceFromAccountingCommand {
                    EngagementId = engagement.Id,
                    AccountingEntityId = Guid.NewGuid(),
                    AccountingPeriodId = Guid.NewGuid()
                }));

            Assert.Empty(_trialBalances.Imports);
        }

        [Fact]
        public async Task An_unknown_entity_or_period_returns_an_error() {
            var user = Member();
            var engagement = SeedEngagement(user.UserId);
            _linked.TrialBalance = null;

            var result = await Harness(user)
                .SendAsync(new ImportTrialBalanceFromAccountingCommand {
                    EngagementId = engagement.Id,
                    AccountingEntityId = Guid.NewGuid(),
                    AccountingPeriodId = Guid.NewGuid()
                });

            Assert.False(result.Successful);
            Assert.Empty(_trialBalances.Imports);
        }

        [Fact]
        public async Task The_context_query_reports_unavailability_without_listing_entities() {
            _linked.Availability = new LinkedAccountingAvailability(false,
                "The organization has not enabled Audit access to its Ledgance Accounting books.");
            _linked.Entities.Add(new LinkedAccountingEntity(Guid.NewGuid(), "Hidden", "PHP",
                []));

            var result = await Harness(Member())
                .SendAsync(new GetLinkedAccountingContextQuery());

            Assert.True(result.Successful);
            Assert.False(result.Data!.IsAvailable);
            Assert.Empty(result.Data.Entities);
            Assert.NotNull(result.Data.UnavailableReason);
        }

        [Fact]
        public async Task The_context_query_lists_entities_when_available() {
            var entityId = Guid.NewGuid();
            _linked.Entities.Add(new LinkedAccountingEntity(entityId, "Acme", "PHP", [
                new LinkedAccountingPeriod(Guid.NewGuid(), "March 2026",
                    new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), "Open")
            ]));

            var result = await Harness(Member())
                .SendAsync(new GetLinkedAccountingContextQuery());

            Assert.True(result.Data!.IsAvailable);
            var entity = Assert.Single(result.Data.Entities);
            Assert.Equal("Acme", entity.Name);
            Assert.Single(entity.Periods);
        }
    }
}
