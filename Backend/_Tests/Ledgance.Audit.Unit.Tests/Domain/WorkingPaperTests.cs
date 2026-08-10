using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Exceptions;

namespace Ledgance.Audit.Unit.Tests.Domain {
    public class WorkingPaperTests {
        private static readonly Guid Preparer = Guid.NewGuid();
        private static readonly Guid Reviewer = Guid.NewGuid();
        private static readonly Guid Approver = Guid.NewGuid();

        private static WorkingPaper NewPaper() =>
            WorkingPaper.Create(Guid.NewGuid(), "B-100", "Cash testing", "Initial content");

        [Fact]
        public void The_preparer_cannot_review_their_own_paper() {
            var paper = NewPaper();
            paper.Prepare(Preparer);

            Assert.Throws<DomainRuleException>(() => paper.Review(Preparer));

            paper.Review(Reviewer);
            Assert.Equal(WorkingPaperStatus.Reviewed, paper.Status);
        }

        [Fact]
        public void The_preparer_cannot_approve_their_own_paper() {
            var paper = NewPaper();
            paper.Prepare(Preparer);
            paper.Review(Reviewer);

            Assert.Throws<DomainRuleException>(() =>
                paper.Approve(Preparer, EngagementRole.Partner));
        }

        [Fact]
        public void Approval_requires_a_manager_or_partner_team_role() {
            var paper = NewPaper();
            paper.Prepare(Preparer);
            paper.Review(Reviewer);

            Assert.Throws<DomainRuleException>(() =>
                paper.Approve(Approver, EngagementRole.Senior));

            paper.Approve(Approver, EngagementRole.Manager);
            Assert.Equal(WorkingPaperStatus.Approved, paper.Status);
        }

        [Fact]
        public void Editing_content_withdraws_existing_sign_offs() {
            var paper = NewPaper();
            paper.Prepare(Preparer);
            paper.Review(Reviewer);

            paper.UpdateContent("Cash testing", "Revised content");

            Assert.Equal(WorkingPaperStatus.Draft, paper.Status);
            Assert.Null(paper.PreparedBy);
            Assert.Null(paper.ReviewedBy);
        }

        [Fact]
        public void An_approved_paper_is_immutable() {
            var paper = NewPaper();
            paper.Prepare(Preparer);
            paper.Review(Reviewer);
            paper.Approve(Approver, EngagementRole.Partner);

            Assert.Throws<DomainRuleException>(() =>
                paper.UpdateContent("Title", "More content"));
            Assert.Throws<DomainRuleException>(() =>
                paper.AddNote(ReviewNote.Raise(Reviewer, "Too late")));
        }

        [Fact]
        public void Open_review_notes_block_approval() {
            var paper = NewPaper();
            var note = ReviewNote.Raise(Reviewer, "Tie this to the bank confirmation.");
            paper.AddNote(note);
            paper.Prepare(Preparer);
            paper.Review(Reviewer);

            Assert.Throws<DomainRuleException>(() =>
                paper.Approve(Approver, EngagementRole.Partner));

            paper.ResolveNote(note.Id, Preparer, "Cross-referenced to B-110.");
            paper.Approve(Approver, EngagementRole.Partner);

            Assert.Equal(WorkingPaperStatus.Approved, paper.Status);
        }

        [Fact]
        public void A_resolved_note_cannot_be_resolved_twice() {
            var paper = NewPaper();
            var note = ReviewNote.Raise(Reviewer, "Check the cutoff.");
            paper.AddNote(note);
            paper.ResolveNote(note.Id, Preparer, "Done.");

            Assert.Throws<DomainRuleException>(() =>
                paper.ResolveNote(note.Id, Preparer, "Again."));
        }

        [Fact]
        public void Sign_off_must_follow_the_prepare_review_approve_order() {
            var paper = NewPaper();

            Assert.Throws<DomainRuleException>(() => paper.Review(Reviewer));
            Assert.Throws<DomainRuleException>(() =>
                paper.Approve(Approver, EngagementRole.Partner));
        }
    }

    public class FindingTests {
        private static Finding NewFinding() =>
            Finding.Raise(Guid.NewGuid(), "Unreconciled cash difference",
                "A 12,000 difference between GL and bank statement.",
                FindingSeverity.High, "Investigate and adjust.", [], Guid.NewGuid());

        [Fact]
        public void Resolving_requires_a_resolution_note() {
            var finding = NewFinding();

            Assert.Throws<DomainRuleException>(() => finding.Resolve(" "));

            finding.Resolve("Client posted the adjusting entry.");
            Assert.Equal(FindingStatus.Resolved, finding.Status);
        }

        [Fact]
        public void An_open_finding_cannot_be_closed_directly() {
            var finding = NewFinding();

            Assert.Throws<DomainRuleException>(finding.Close);

            finding.Resolve("Adjusted.");
            finding.Close();
            Assert.Equal(FindingStatus.Closed, finding.Status);
        }

        [Fact]
        public void Risk_acceptance_requires_a_justification_and_can_be_closed() {
            var finding = NewFinding();

            Assert.Throws<DomainRuleException>(() => finding.AcceptRisk(""));

            finding.AcceptRisk("Immaterial to the financial statements as a whole.");
            finding.Close();

            Assert.Equal(FindingStatus.Closed, finding.Status);
        }
    }

    public class AuditReportTests {
        [Fact]
        public void Only_a_partner_can_finalize() {
            var report = AuditReport.Draft(Guid.NewGuid());

            Assert.Throws<DomainRuleException>(() =>
                report.Finalize(Guid.NewGuid(), EngagementRole.Manager, 0));
        }

        [Fact]
        public void Open_findings_block_finalization() {
            var report = AuditReport.Draft(Guid.NewGuid());

            Assert.Throws<DomainRuleException>(() =>
                report.Finalize(Guid.NewGuid(), EngagementRole.Partner, 2));
        }

        [Fact]
        public void A_modified_opinion_requires_a_basis() {
            var report = AuditReport.Draft(Guid.NewGuid());
            report.UpdateDraft(AuditOpinion.Qualified, "", "", "");

            Assert.Throws<DomainRuleException>(() =>
                report.Finalize(Guid.NewGuid(), EngagementRole.Partner, 0));

            report.UpdateDraft(AuditOpinion.Qualified,
                "Inventory could not be observed at year end.", "", "");
            report.Finalize(Guid.NewGuid(), EngagementRole.Partner, 0);

            Assert.True(report.IsFinalized);
        }

        [Fact]
        public void A_finalized_report_cannot_be_edited() {
            var report = AuditReport.Draft(Guid.NewGuid());
            report.Finalize(Guid.NewGuid(), EngagementRole.Partner, 0);

            Assert.Throws<DomainRuleException>(() =>
                report.UpdateDraft(AuditOpinion.Adverse, "Basis", "", ""));
        }
    }

    public class TrialBalanceTests {
        [Fact]
        public void Totals_and_balance_state_are_computed_from_the_lines() {
            var import = TrialBalanceImport.Create(Guid.NewGuid(),
                TrialBalanceSource.ExternalCsv, "FY2026",
                [
                    new TrialBalanceLine("1000", "Cash", 500, 0),
                    new TrialBalanceLine("3000", "Equity", 0, 500)
                ], Guid.NewGuid());

            Assert.Equal(500, import.TotalDebits);
            Assert.Equal(500, import.TotalCredits);
            Assert.True(import.IsBalanced);
        }

        [Fact]
        public void An_out_of_balance_import_is_kept_but_flagged() {
            var import = TrialBalanceImport.Create(Guid.NewGuid(),
                TrialBalanceSource.ExternalCsv, "FY2026",
                [
                    new TrialBalanceLine("1000", "Cash", 500, 0),
                    new TrialBalanceLine("3000", "Equity", 0, 400)
                ], Guid.NewGuid());

            Assert.False(import.IsBalanced);
        }

        [Fact]
        public void Empty_imports_and_negative_amounts_are_rejected() {
            Assert.Throws<DomainRuleException>(() => TrialBalanceImport.Create(
                Guid.NewGuid(), TrialBalanceSource.ExternalCsv, "FY2026", [], Guid.NewGuid()));

            Assert.Throws<DomainRuleException>(() => TrialBalanceImport.Create(
                Guid.NewGuid(), TrialBalanceSource.ExternalCsv, "FY2026",
                [new TrialBalanceLine("1000", "Cash", -1, 0)], Guid.NewGuid()));
        }
    }

    public class TeamRulesTests {
        [Fact]
        public void The_last_partner_cannot_be_removed() {
            var engagementId = Guid.NewGuid();
            var partner = EngagementTeamMember.Assign(engagementId, Guid.NewGuid(),
                EngagementRole.Partner);
            var staff = EngagementTeamMember.Assign(engagementId, Guid.NewGuid(),
                EngagementRole.Staff);
            var team = new[] { partner, staff };

            Assert.Throws<DomainRuleException>(() =>
                TeamRules.EnsureCanRemove(team, partner.Id));

            TeamRules.EnsureCanRemove(team, staff.Id);
        }

        [Fact]
        public void A_user_cannot_be_assigned_twice() {
            var engagementId = Guid.NewGuid();
            var member = EngagementTeamMember.Assign(engagementId, Guid.NewGuid(),
                EngagementRole.Senior);

            Assert.Throws<DomainRuleException>(() =>
                TeamRules.EnsureCanAssign([member], member.UserId));
        }
    }
}
