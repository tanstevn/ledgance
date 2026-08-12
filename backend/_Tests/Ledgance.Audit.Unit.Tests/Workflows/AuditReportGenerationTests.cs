using Ledgance.Audit.AI.Application;
using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.AI.Application.Reporting;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Ai;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    /// <summary>
    /// Report generation exercised against the real AI completion service, so the plan gate the
    /// production path applies is the one under test. A test that stubbed the AI service would
    /// prove only that the handler runs — not that a Free organization is actually refused.
    /// </summary>
    public class AuditReportGenerationTests {
        private const string SectionJson = """
            {"sections":[
              {"section":"ExecutiveSummary","heading":"Executive summary",
               "content":"The engagement is substantially complete.",
               "sources":["Finding: Unreconciled bank balance"]},
              {"section":"Findings","heading":"Findings",
               "content":"One finding was raised.","sources":["Finding: Unreconciled bank balance"]}
            ]}
            """;

        private readonly InMemoryEngagementRepository _engagements = new();
        private readonly InMemoryTeamRepository _team = new();
        private readonly InMemoryRiskRepository _risks = new();
        private readonly InMemoryProcedureRepository _procedures = new();
        private readonly InMemoryWorkingPaperRepository _papers = new();
        private readonly InMemoryEvidenceRepository _evidence = new();
        private readonly InMemoryFindingRepository _findings = new();
        private readonly InMemoryTrialBalanceRepository _trialBalances = new();
        private readonly StubClientLookup _clients = new();
        private readonly InMemoryGeneratedReportRepository _reports = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly StubAiUsagePeriodResolver _periods = new();
        private readonly StubAiOperationCosts _costs = new();
        private readonly RecordingActivityRecorder _activity = new();
        private readonly FakeAiChatClient _provider =
            new(AiProviders.Ollama, _ => SectionJson);
        private readonly FakeAiChatClient _openAi = new(AiProviders.OpenAI, _ => SectionJson);
        private readonly FakeAiChatClient _anthropic =
            new(AiProviders.Anthropic, _ => SectionJson);

        private DomainEngagement Seed(Guid? teamUserId = null,
            EngagementRole role = EngagementRole.Senior) {
            var engagement = DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());

            _engagements.Engagements.Add(engagement);
            _clients.ActiveClients.Add(engagement.ClientId);

            if (teamUserId is not null) {
                _team.Members.Add(EngagementTeamMember.Assign(engagement.Id, teamUserId.Value,
                    role));
            }

            return engagement;
        }

        private MediatorTestHarness Harness(CurrentUser user, PlanCode plan) {
            var harness = new MediatorTestHarness(user)
                .WithHandler<GenerateReportSectionCommand, Result<AiProposalResult>,
                    GenerateReportSectionCommandHandler>()
                .WithHandler<GenerateDraftReportCommand, Result<GeneratedReportView>,
                    GenerateDraftReportCommandHandler>()
                .WithHandler<GenerateEngagementReportCommand, Result<GeneratedReportView>,
                    GenerateEngagementReportCommandHandler>()
                .WithHandler<CheckReportConsistencyCommand, Result<AiProposalResult>,
                    CheckReportConsistencyCommandHandler>()
                .WithHandler<RegenerateReportSectionCommand, Result<GeneratedReportView>,
                    RegenerateReportSectionCommandHandler>()
                .WithHandler<ReviewGeneratedReportCommand, Result<GeneratedReportView>,
                    ReviewGeneratedReportCommandHandler>()
                .WithHandler<GetGeneratedReportsQuery, Result<IEnumerable<GeneratedReportView>>,
                    GetGeneratedReportsQueryHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<ITeamRepository>(_team)
                .WithService<IRiskRepository>(_risks)
                .WithService<IProcedureRepository>(_procedures)
                .WithService<IWorkingPaperRepository>(_papers)
                .WithService<IEvidenceRepository>(_evidence)
                .WithService<IFindingRepository>(_findings)
                .WithService<ITrialBalanceRepository>(_trialBalances)
                .WithService<IClientLookup>(_clients)
                .WithService<IGeneratedReportRepository>(_reports)
                .WithService<IActivityRecorder>(_activity);

            harness.Entitlements.With(ProductModule.Audit, plan);

            harness.WithService(new EngagementReadSet(_engagements, _clients, _risks,
                _procedures, _papers, _evidence, _findings, _trialBalances));

            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));

            harness.WithService<IAiCompletionService>(new AiCompletionService(
                harness.CurrentUser, harness.Entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_provider, _openAi, _anthropic],
                NullLogger<AiCompletionService>.Instance));

            return harness;
        }

        private static CurrentUser Contributor() =>
            TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read,
                    AuditEngagementPermissions.Contribute,
                    AuditEngagementPermissions.Manage,
                    AuditEngagementPermissions.Approve]);

        [Fact]
        public async Task Free_cannot_generate_a_report_section_by_calling_the_endpoint() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            await Assert.ThrowsAsync<EntitlementException>(() =>
                Harness(user, PlanCode.Free).SendAsync(new GenerateReportSectionCommand {
                    EngagementId = engagement.Id,
                    Section = AuditReportSection.ExecutiveSummary
                }));

            Assert.Empty(_provider.Calls);
            Assert.Empty(_reports.Reports);
        }

        [Fact]
        public async Task Free_cannot_generate_a_complete_draft_report() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            await Assert.ThrowsAsync<EntitlementException>(() =>
                Harness(user, PlanCode.Free).SendAsync(new GenerateDraftReportCommand {
                    EngagementId = engagement.Id
                }));

            Assert.Empty(_reports.Reports);
        }

        [Fact]
        public async Task Micro_generates_a_section_but_still_not_a_whole_report() {
            var user = Contributor();
            var engagement = Seed(user.UserId);
            var harness = Harness(user, PlanCode.AuditMicro);

            var section = await harness.SendAsync(new GenerateReportSectionCommand {
                EngagementId = engagement.Id,
                Section = AuditReportSection.Recommendations
            });

            Assert.True(section.Successful);
            Assert.Equal(AuditAiCapabilities.ReportSection.Key, section.Data!.Capability);

            await Assert.ThrowsAsync<EntitlementException>(() =>
                harness.SendAsync(new GenerateDraftReportCommand {
                    EngagementId = engagement.Id
                }));
        }

        [Fact]
        public async Task Micro_growth_generates_a_complete_draft_that_awaits_review() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            var result = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            Assert.True(result.Successful);
            Assert.Equal(nameof(GeneratedReportStatus.Draft), result.Data!.Status);
            Assert.Equal(2, result.Data.Sections.Count);
            Assert.Contains("carries no audit opinion", result.Data.Disclaimer);
            Assert.Contains("must review every section", result.Data.Disclaimer);

            var stored = Assert.Single(_reports.Reports);
            Assert.True(stored.IsAwaitingReview);
            Assert.Contains(_activity.Entries, entry => entry.Action == "ai.report_generated");
        }

        /// <summary>
        /// Traceability: the sources the model names travel with the section, so a reviewer can
        /// follow a claim back to the record it came from.
        /// </summary>
        [Fact]
        public async Task A_generated_section_keeps_the_engagement_records_it_cites() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            var result = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            Assert.All(result.Data!.Sections, section => Assert.NotEmpty(section.Sources));
        }

        [Fact]
        public async Task Micro_growth_cannot_generate_the_full_engagement_report() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            await Assert.ThrowsAsync<EntitlementException>(() =>
                Harness(user, PlanCode.AuditMicroGrowth)
                    .SendAsync(new GenerateEngagementReportCommand {
                        EngagementId = engagement.Id
                    }));
        }

        [Fact]
        public async Task Small_generates_the_full_engagement_report() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            var result = await Harness(user, PlanCode.AuditSmall)
                .SendAsync(new GenerateEngagementReportCommand {
                    EngagementId = engagement.Id,
                    ForReviewer = true
                });

            Assert.True(result.Successful);
            Assert.Equal(AuditAiCapabilities.EngagementReport.Key, result.Data!.Capability);
            Assert.Equal(AiReportScopes.Engagement, result.Data.ReportScope);
        }

        [Fact]
        public async Task A_non_team_member_cannot_generate_a_report_for_the_engagement() {
            var user = Contributor();
            var engagement = Seed();

            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Harness(user, PlanCode.AuditSmall)
                    .SendAsync(new GenerateDraftReportCommand {
                        EngagementId = engagement.Id
                    }));

            Assert.Empty(_reports.Reports);
        }

        [Fact]
        public async Task A_generated_report_cannot_be_read_through_another_engagement() {
            var user = Contributor();
            var mine = Seed(user.UserId);
            var other = Seed(user.UserId);

            var generated = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = mine.Id });

            var harness = Harness(user, PlanCode.AuditMicroGrowth);

            var wrongEngagement = await harness.SendAsync(new CheckReportConsistencyCommand {
                EngagementId = other.Id,
                ReportId = generated.Data!.Id
            });

            Assert.False(wrongEngagement.Successful);

            var listed = await harness.SendAsync(new GetGeneratedReportsQuery {
                EngagementId = other.Id
            });

            Assert.Empty(listed.Data!);
        }

        [Fact]
        public async Task A_senior_cannot_accept_a_generated_report() {
            var user = Contributor();
            var engagement = Seed(user.UserId, EngagementRole.Senior);

            var generated = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            await Assert.ThrowsAsync<DomainRuleException>(() =>
                Harness(user, PlanCode.AuditMicroGrowth)
                    .SendAsync(new ReviewGeneratedReportCommand {
                        EngagementId = engagement.Id,
                        ReportId = generated.Data!.Id,
                        Accept = true
                    }));

            Assert.True(_reports.Reports.Single().IsAwaitingReview);
        }

        [Fact]
        public async Task A_partner_accepts_the_draft_and_the_acceptance_is_recorded() {
            var user = Contributor();
            var engagement = Seed(user.UserId, EngagementRole.Partner);

            var generated = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            var reviewed = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new ReviewGeneratedReportCommand {
                    EngagementId = engagement.Id,
                    ReportId = generated.Data!.Id,
                    Accept = true,
                    Note = "Checked against the file."
                });

            Assert.True(reviewed.Successful);
            Assert.Equal(nameof(GeneratedReportStatus.Accepted), reviewed.Data!.Status);
            Assert.Equal(user.UserId, reviewed.Data.ReviewedBy);
            Assert.Contains(_activity.Entries, entry => entry.Action == "ai.report_accepted");
        }

        [Fact]
        public async Task An_accepted_draft_cannot_be_reviewed_a_second_time() {
            var user = Contributor();
            var engagement = Seed(user.UserId, EngagementRole.Partner);

            var generated = await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            var harness = Harness(user, PlanCode.AuditMicroGrowth);

            await harness.SendAsync(new ReviewGeneratedReportCommand {
                EngagementId = engagement.Id,
                ReportId = generated.Data!.Id,
                Accept = true
            });

            await Assert.ThrowsAsync<DomainRuleException>(() =>
                harness.SendAsync(new ReviewGeneratedReportCommand {
                    EngagementId = engagement.Id,
                    ReportId = generated.Data.Id,
                    Accept = false,
                    Note = "Changed my mind."
                }));
        }

        /// <summary>
        /// Regenerating stores a new draft rather than editing the one a reviewer may already be
        /// looking at.
        /// </summary>
        [Fact]
        public async Task Regenerating_a_section_leaves_the_reviewed_version_untouched() {
            var user = Contributor();
            var engagement = Seed(user.UserId);
            var harness = Harness(user, PlanCode.AuditMicroGrowth);

            var original = await harness.SendAsync(new GenerateDraftReportCommand {
                EngagementId = engagement.Id
            });

            var regenerated = await harness.SendAsync(new RegenerateReportSectionCommand {
                EngagementId = engagement.Id,
                ReportId = original.Data!.Id,
                Section = AuditReportSection.Findings
            });

            Assert.True(regenerated.Successful);
            Assert.NotEqual(original.Data.Id, regenerated.Data!.Id);
            Assert.Equal(2, _reports.Reports.Count);
            Assert.All(_reports.Reports, report => Assert.True(report.IsAwaitingReview));
        }

        /// <summary>
        /// A provider that ignores the JSON contract must still produce something a reviewer can
        /// look at, rather than failing the generation outright.
        /// </summary>
        [Fact]
        public async Task Prose_from_the_provider_still_yields_a_reviewable_draft() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            _provider.Throws = null;
            var prose = new FakeAiChatClient(AiProviders.OpenAI,
                _ => "The engagement is complete and no matters were noted.");

            var harness = new MediatorTestHarness(user)
                .WithHandler<GenerateDraftReportCommand, Result<GeneratedReportView>,
                    GenerateDraftReportCommandHandler>()
                .WithService<IEngagementRepository>(_engagements)
                .WithService<ITeamRepository>(_team)
                .WithService<IClientLookup>(_clients)
                .WithService<IGeneratedReportRepository>(_reports)
                .WithService<IActivityRecorder>(_activity);

            harness.Entitlements.With(ProductModule.Audit, PlanCode.AuditMicroGrowth);

            harness.WithService(new EngagementReadSet(_engagements, _clients, _risks,
                _procedures, _papers, _evidence, _findings, _trialBalances));
            harness.WithService<IEngagementAccessGuard>(
                new EngagementAccessGuard(_team, harness.CurrentUser));
            harness.WithService<IAiCompletionService>(new AiCompletionService(
                harness.CurrentUser, harness.Entitlements, _usage, _periods, _costs,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [prose], NullLogger<AiCompletionService>.Instance));

            var result = await harness.SendAsync(new GenerateDraftReportCommand {
                EngagementId = engagement.Id
            });

            Assert.True(result.Successful);
            var section = Assert.Single(result.Data!.Sections);
            Assert.Contains("no matters were noted", section.Content);
        }

        /// <summary>
        /// The reporting prompts must carry the do-not-fabricate rules; without them the model
        /// is free to fill gaps with plausible text, which is the failure mode that matters most.
        /// </summary>
        [Fact]
        public async Task Report_prompts_forbid_inventing_what_the_record_does_not_contain() {
            var user = Contributor();
            var engagement = Seed(user.UserId);

            await Harness(user, PlanCode.AuditMicroGrowth)
                .SendAsync(new GenerateDraftReportCommand { EngagementId = engagement.Id });

            var systemPrompt = Assert.Single(_openAi.Calls).SystemPrompt;

            Assert.Contains("Never invent evidence", systemPrompt);
            Assert.Contains("NOT IN THE ENGAGEMENT RECORD", systemPrompt);
            Assert.Contains("PARTNER JUDGMENT", systemPrompt);
        }
    }
}
