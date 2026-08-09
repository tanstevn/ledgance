using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Audit.Engagement.Application.Engagements;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    public class EngagementWorkflowTests {
        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly StubClientLookup _clients = new();
        private readonly StubProgressReader _progress = new();
        private readonly RecordingActivityRecorder _activity = new();

        private MediatorTestHarness Harness(CurrentUser user) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<CreateEngagementCommand, Result<CreateEngagementCommandResult>,
                    CreateEngagementCommandHandler>()
                .WithHandler<ChangeEngagementStatusCommand, Result<string>,
                    ChangeEngagementStatusCommandHandler>()
                .WithHandler<GetEngagementsQuery, Result<IEnumerable<EngagementListRow>>,
                    GetEngagementsQueryHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<ITeamRepository>(_team)
                .WithService<IClientLookup>(_clients)
                .WithService<IEngagementProgressReader>(_progress)
                .WithService<IActivityRecorder>(_activity);

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            return harness;
        }

        private static CurrentUser Manager() =>
            TestIdentity.User(OrganizationRole.Manager,
                permissions: [AuditEngagementPermissions.Manage,
                    AuditEngagementPermissions.Contribute, AuditEngagementPermissions.Read]);

        private static CreateEngagementCommand ValidCreate(Guid clientId) =>
            new() {
                ClientId = clientId,
                Name = "FY2026 Financial Statement Audit",
                Type = EngagementType.FinancialStatement,
                PeriodStart = new DateOnly(2026, 1, 1),
                PeriodEnd = new DateOnly(2026, 12, 31),
                BudgetHours = 200
            };

        [Fact]
        public async Task An_engagement_cannot_reference_a_missing_or_archived_client() {
            var harness = Harness(Manager());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);

            var result = await harness.SendAsync(ValidCreate(Guid.NewGuid()));

            Assert.False(result.Successful);
            Assert.Empty(_engagements.Engagements);
        }

        [Fact]
        public async Task Creating_an_engagement_assigns_the_creator_as_partner() {
            var user = Manager();
            var harness = Harness(user);
            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditProfessional);

            var clientId = Guid.NewGuid();
            _clients.ActiveClients.Add(clientId);

            var result = await harness.SendAsync(ValidCreate(clientId));

            Assert.True(result.Successful);
            var member = Assert.Single(_team.Members);
            Assert.Equal(user.UserId, member.UserId);
            Assert.Equal(EngagementRole.Partner, member.Role);
        }

        [Fact]
        public async Task The_free_plan_engagement_limit_is_enforced() {
            var harness = Harness(Manager());
            harness.Entitlements.With(ProductModule.Audit, PlanCode.Free);

            var clientId = Guid.NewGuid();
            _clients.ActiveClients.Add(clientId);

            Assert.True((await harness.SendAsync(ValidCreate(clientId))).Successful);
            Assert.True((await harness.SendAsync(ValidCreate(clientId))).Successful);

            await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(ValidCreate(clientId)));

            Assert.Equal(2, _engagements.Engagements.Count);
        }

        [Fact]
        public async Task A_non_team_member_cannot_touch_an_engagement() {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026",
                EngagementType.Internal, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 0, Guid.NewGuid());
            _engagements.Engagements.Add(engagement);

            var outsider = TestIdentity.User(OrganizationRole.Manager,
                permissions: [AuditEngagementPermissions.Contribute]);
            var harness = Harness(outsider);

            await Assert.ThrowsAsync<ForbiddenException>(() => harness.SendAsync(
                new ChangeEngagementStatusCommand {
                    Id = engagement.Id,
                    TargetStatus = EngagementStatus.Fieldwork
                }));
        }

        [Fact]
        public async Task An_org_admin_retains_oversight_access_without_assignment() {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026",
                EngagementType.Internal, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 0, Guid.NewGuid());
            engagement.SavePlan("Scope", "Objectives", "Strategy", null, null);
            engagement.ApprovePlan(Guid.NewGuid());
            engagement.SetMateriality(Materiality.Create(100_000, 70_000, 5_000, "Rev", "Why"));
            _engagements.Engagements.Add(engagement);

            var admin = TestIdentity.User(OrganizationRole.Admin,
                permissions: [AuditEngagementPermissions.Contribute]);
            var harness = Harness(admin);

            var result = await harness.SendAsync(new ChangeEngagementStatusCommand {
                Id = engagement.Id,
                TargetStatus = EngagementStatus.Fieldwork
            });

            Assert.True(result.Successful);
            Assert.Equal(nameof(EngagementStatus.Fieldwork), result.Data);
        }

        [Fact]
        public async Task Sign_off_fails_for_an_admin_who_is_not_the_engagement_partner() {
            var engagement = DomainEngagement.Restore(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
                EngagementType.Internal, EngagementStatus.Review, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 0, Guid.NewGuid(), DateTime.UtcNow,
                null, null);
            _engagements.Engagements.Add(engagement);

            var admin = TestIdentity.User(OrganizationRole.Admin,
                permissions: [AuditEngagementPermissions.Contribute]);
            var harness = Harness(admin);

            await Assert.ThrowsAsync<DomainRuleException>(() => harness.SendAsync(
                new ChangeEngagementStatusCommand {
                    Id = engagement.Id,
                    TargetStatus = EngagementStatus.SignedOff
                }));
        }

        [Fact]
        public async Task A_team_partner_signs_off_when_every_gate_is_clear() {
            var user = Manager();
            var engagement = DomainEngagement.Restore(Guid.NewGuid(), Guid.NewGuid(), "FY2026",
                EngagementType.Internal, EngagementStatus.Review, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 0, user.UserId, DateTime.UtcNow, null, null);
            _engagements.Engagements.Add(engagement);
            _team.Members.Add(EngagementTeamMember.Assign(engagement.Id, user.UserId,
                EngagementRole.Partner));

            var harness = Harness(user);

            var result = await harness.SendAsync(new ChangeEngagementStatusCommand {
                Id = engagement.Id,
                TargetStatus = EngagementStatus.SignedOff
            });

            Assert.True(result.Successful);
            Assert.Equal(nameof(EngagementStatus.SignedOff), result.Data);
        }
    }

    public class CsvTrialBalanceParsingTests {
        private readonly CsvAccountingContextSource _source = new();

        [Fact]
        public async Task Parses_rows_and_skips_a_header() {
            var lines = await _source.ReadTrialBalanceAsync(
                "Account Code,Account Name,Debit,Credit\n" +
                "1000,Cash and equivalents,\"12,500.50\",0\n" +
                "3000,Share capital,0,12500.50\n", default);

            Assert.Equal(2, lines.Count);
            Assert.Equal(12500.50m, lines[0].Debit);
            Assert.Equal("Cash and equivalents", lines[0].AccountName);
        }

        [Fact]
        public async Task Rejects_rows_with_missing_columns() {
            await Assert.ThrowsAsync<DomainRuleException>(
                () => _source.ReadTrialBalanceAsync("1000,Cash,100\n", default));
        }

        [Fact]
        public async Task Rejects_non_numeric_amounts_outside_the_header() {
            await Assert.ThrowsAsync<DomainRuleException>(
                () => _source.ReadTrialBalanceAsync(
                    "1000,Cash,abc,0\n2000,Loans,0,50\n", default));
        }
    }
}
