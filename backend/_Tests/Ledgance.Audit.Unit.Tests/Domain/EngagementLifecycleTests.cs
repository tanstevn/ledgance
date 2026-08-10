using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Exceptions;
using DomainEngagement = Ledgance.Audit.Engagement.Domain.Engagement;

namespace Ledgance.Audit.Unit.Tests.Domain {
    public class EngagementLifecycleTests {
        private static DomainEngagement NewEngagement() =>
            DomainEngagement.Create(Guid.NewGuid(), "FY2026 Audit",
                EngagementType.FinancialStatement, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 100, Guid.NewGuid());

        private static DomainEngagement ReadyForFieldwork() {
            var engagement = NewEngagement();
            engagement.SavePlan("Full scope", "Fair presentation", "Risk-based", null, null);
            engagement.ApprovePlan(Guid.NewGuid());
            engagement.SetMateriality(Materiality.Create(100_000, 70_000, 5_000,
                "Total revenue", "Standard benchmark"));
            return engagement;
        }

        private static readonly EngagementProgress CleanProgress = new(0, 0, 0, 0, 0, true);

        [Fact]
        public void An_engagement_period_must_end_after_it_starts() {
            Assert.Throws<DomainRuleException>(() => DomainEngagement.Create(Guid.NewGuid(),
                "Bad", EngagementType.Internal, new DateOnly(2026, 12, 31),
                new DateOnly(2026, 1, 1), null, 0, Guid.NewGuid()));
        }

        [Fact]
        public void Fieldwork_requires_an_approved_plan_and_materiality() {
            var engagement = NewEngagement();

            Assert.Throws<DomainRuleException>(engagement.StartFieldwork);

            engagement.SavePlan("Scope", "Objectives", "Strategy", null, null);
            Assert.Throws<DomainRuleException>(engagement.StartFieldwork);

            engagement.ApprovePlan(Guid.NewGuid());
            Assert.Throws<DomainRuleException>(engagement.StartFieldwork);

            engagement.SetMateriality(Materiality.Create(100_000, 70_000, 5_000, "Revenue", "Why"));
            engagement.StartFieldwork();

            Assert.Equal(EngagementStatus.Fieldwork, engagement.Status);
        }

        [Fact]
        public void Editing_an_approved_plan_withdraws_its_approval() {
            var engagement = ReadyForFieldwork();

            engagement.SavePlan("Wider scope", "New objectives", "Strategy", null, null);

            Assert.False(engagement.Plan!.IsApproved);
            Assert.Throws<DomainRuleException>(engagement.StartFieldwork);
        }

        [Fact]
        public void Review_submission_is_blocked_while_procedures_are_open() {
            var engagement = ReadyForFieldwork();
            engagement.StartFieldwork();

            Assert.Throws<DomainRuleException>(() =>
                engagement.SubmitForReview(CleanProgress with { OpenProcedures = 2 }));

            engagement.SubmitForReview(CleanProgress);
            Assert.Equal(EngagementStatus.Review, engagement.Status);
        }

        [Fact]
        public void Sign_off_requires_the_partner_role() {
            var engagement = ReadyForFieldwork();
            engagement.StartFieldwork();
            engagement.SubmitForReview(CleanProgress);

            Assert.Throws<DomainRuleException>(() =>
                engagement.SignOff(CleanProgress, EngagementRole.Manager));
            Assert.Throws<DomainRuleException>(() =>
                engagement.SignOff(CleanProgress, null));

            engagement.SignOff(CleanProgress, EngagementRole.Partner);
            Assert.Equal(EngagementStatus.SignedOff, engagement.Status);
        }

        [Theory]
        [InlineData(1, 0, 0, 0, 0)]
        [InlineData(0, 1, 0, 0, 0)]
        [InlineData(0, 0, 1, 0, 0)]
        [InlineData(0, 0, 0, 1, 0)]
        [InlineData(0, 0, 0, 0, 1)]
        public void Sign_off_is_blocked_by_any_open_item(int papers, int notes, int findings,
            int highRisks, int openProcedures) {
            var engagement = ReadyForFieldwork();
            engagement.StartFieldwork();
            engagement.SubmitForReview(CleanProgress);

            var progress = new EngagementProgress(openProcedures, papers, notes, findings,
                highRisks, true);

            if (progress == CleanProgress) {
                return;
            }

            Assert.Throws<DomainRuleException>(() =>
                engagement.SignOff(progress, EngagementRole.Partner));
        }

        [Fact]
        public void Completion_requires_a_finalized_report() {
            var engagement = ReadyForFieldwork();
            engagement.StartFieldwork();
            engagement.SubmitForReview(CleanProgress);
            engagement.SignOff(CleanProgress, EngagementRole.Partner);

            Assert.Throws<DomainRuleException>(() =>
                engagement.Complete(CleanProgress with { ReportFinalized = false }));

            engagement.Complete(CleanProgress);
            Assert.Equal(EngagementStatus.Completed, engagement.Status);
        }

        [Fact]
        public void A_signed_off_engagement_rejects_modification() {
            var engagement = ReadyForFieldwork();
            engagement.StartFieldwork();
            engagement.SubmitForReview(CleanProgress);
            engagement.SignOff(CleanProgress, EngagementRole.Partner);

            Assert.Throws<DomainRuleException>(() => engagement.UpdateDetails("New name",
                EngagementType.Internal, new DateOnly(2026, 1, 1),
                new DateOnly(2026, 12, 31), null, 10));
            Assert.Throws<DomainRuleException>(() =>
                engagement.SetMateriality(Materiality.Create(1_000, 700, 50, "B", "R")));
        }

        [Fact]
        public void Stages_cannot_be_skipped() {
            var engagement = ReadyForFieldwork();

            Assert.Throws<DomainRuleException>(() =>
                engagement.SubmitForReview(CleanProgress));
            Assert.Throws<DomainRuleException>(() =>
                engagement.SignOff(CleanProgress, EngagementRole.Partner));
            Assert.Throws<DomainRuleException>(() => engagement.Complete(CleanProgress));
        }
    }

    public class MaterialityTests {
        [Fact]
        public void Performance_materiality_must_stay_below_overall() {
            Assert.Throws<DomainRuleException>(() =>
                Materiality.Create(100_000, 100_000, 5_000, "Revenue", "Why"));
        }

        [Fact]
        public void The_clearly_trivial_threshold_must_stay_below_performance() {
            Assert.Throws<DomainRuleException>(() =>
                Materiality.Create(100_000, 70_000, 70_000, "Revenue", "Why"));
        }

        [Fact]
        public void Amounts_must_be_positive() {
            Assert.Throws<DomainRuleException>(() =>
                Materiality.Create(0, 0, 0, "Revenue", "Why"));
        }

        [Fact]
        public void A_basis_and_rationale_are_required() {
            Assert.Throws<DomainRuleException>(() =>
                Materiality.Create(100_000, 70_000, 5_000, "", "Why"));
            Assert.Throws<DomainRuleException>(() =>
                Materiality.Create(100_000, 70_000, 5_000, "Revenue", " "));
        }
    }

    public class RiskLevelTests {
        [Theory]
        [InlineData(RiskRating.Low, RiskRating.Low, RiskLevel.Low)]
        [InlineData(RiskRating.Low, RiskRating.Medium, RiskLevel.Low)]
        [InlineData(RiskRating.Low, RiskRating.High, RiskLevel.Medium)]
        [InlineData(RiskRating.Medium, RiskRating.Medium, RiskLevel.Medium)]
        [InlineData(RiskRating.Medium, RiskRating.High, RiskLevel.High)]
        [InlineData(RiskRating.High, RiskRating.High, RiskLevel.High)]
        public void Risk_level_combines_likelihood_and_impact(RiskRating likelihood,
            RiskRating impact, RiskLevel expected) {
            Assert.Equal(expected, Risk.LevelOf(likelihood, impact));
        }
    }
}
