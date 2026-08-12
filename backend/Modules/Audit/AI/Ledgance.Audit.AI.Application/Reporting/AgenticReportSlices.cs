using FluentValidation;
using Ledgance.Audit.AI.Application.Agent;
using Ledgance.Audit.AI.Application.Ports;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Application.Reporting {
    public class AgenticReportResult {
        public GeneratedReportView Report { get; set; } = new();
        public List<AgentStepView> Steps { get; set; } = [];
        public int TurnsUsed { get; set; }
        public AiUsageView? Usage { get; set; }
    }

    [RequiresPermission(AuditEngagementPermissions.Manage)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class RunAgenticReportWorkflowCommand : ICommand<Result<AgenticReportResult>> {
        public Guid EngagementId { get; set; }
        public string? Instruction { get; set; }
    }

    public class RunAgenticReportWorkflowCommandValidator
        : AbstractValidator<RunAgenticReportWorkflowCommand> {
        public RunAgenticReportWorkflowCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Instruction).MaximumLength(2000);
        }
    }

    /// <summary>
    /// Agentic report generation: instead of being handed the engagement record, the agent
    /// gathers it itself through the same read-only, authorization-checked tools the
    /// investigation agent uses, then drafts, then checks its own draft back against what it
    /// read. The tool set is fixed to one engagement, so the agent can never widen its own
    /// scope, and the result is still only a draft awaiting professional review.
    /// </summary>
    public class RunAgenticReportWorkflowCommandHandler
        : IRequestHandler<RunAgenticReportWorkflowCommand, Result<AgenticReportResult>> {
        private const string SystemPrompt =
            "You are an AI agent generating a draft audit report inside the Ledgance Audit " +
            "platform, for a professional audit team. Work only from what your tools return — " +
            "they are your only source of engagement data and they enforce the caller's " +
            "authorization. Follow this sequence and do not skip a step: retrieve the " +
            "engagement overview; review the identified risks; review the procedures and " +
            "their conclusions; review the evidence obtained; review the findings raised; " +
            "note what is unresolved; decide which report sections the record supports; draft " +
            "the report; then re-read what your tools returned and check your own draft " +
            "against it. Never invent evidence, procedures, findings, amounts, client details " +
            "or conclusions. Where the record does not support a section, write " +
            "'[NOT IN THE ENGAGEMENT RECORD: <what is missing>]' rather than filling the gap. " +
            "Reserve every audit opinion for the engagement partner and mark those places " +
            "'[PARTNER JUDGMENT]'. Your output is a draft for professional review, never a " +
            "conclusion of record.";

        private readonly IAgentRunner _agent;
        private readonly IMediator _mediator;
        private readonly IEngagementAccessGuard _access;
        private readonly IGeneratedReportRepository _reports;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public RunAgenticReportWorkflowCommandHandler(IAgentRunner agent, IMediator mediator,
            IEngagementAccessGuard access, IGeneratedReportRepository reports,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _agent = agent;
            _mediator = mediator;
            _access = access;
            _reports = reports;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<AgenticReportResult>> HandleAsync(
            RunAgenticReportWorkflowCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var goal =
                "Generate the draft audit report for this engagement. Gather the engagement " +
                "record with your tools first, then draft, then check the draft against what " +
                "you gathered.\n\n" +
                (request.Instruction is null ? "" : $"{request.Instruction}\n\n") +
                ReportComposition.FormatInstruction(ReportSectionSets.Engagement) +
                "\nYour final answer must be that JSON object and nothing else.";

            var run = await _agent.RunAsync(new AgentWorkload(ProductModule.Audit,
                AuditAiCapabilities.AgenticReport.Key, goal, SystemPrompt,
                AuditAgentTools.ForEngagement(_mediator, request.EngagementId),
                MaxToolSteps: 12,
                RequiredReportScope: AuditAiCapabilities.AgenticReport.RequiredReportScope,
                Cost: AuditAiCapabilities.AgenticReport.Cost,
                EngagementId: request.EngagementId), ct);

            var report = GeneratedAuditReport.Draft(request.EngagementId,
                AuditAiCapabilities.AgenticReport.Key,
                AuditAiCapabilities.AgenticReport.RequiredReportScope,
                "Agent-generated audit report",
                ReportComposition.Parse(run.Answer, ReportSectionSets.Engagement),
                run.Provider, run.Model, _currentUser.Require().UserId);

            await _reports.AddAsync(report, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.agentic_report",
                "GeneratedAuditReport", report.Id,
                $"ran the agentic report workflow over {run.Steps.Count} tool steps using " +
                $"{run.Provider}/{run.Model}; the draft is awaiting professional review.",
                request.EngagementId), ct);

            return Result<AgenticReportResult>.Success(new AgenticReportResult {
                Report = GeneratedReportView.From(report),
                Steps = [.. run.Steps.Select(step => new AgentStepView {
                    Tool = step.Tool,
                    Arguments = step.ArgumentsJson,
                    Result = step.Result
                })],
                TurnsUsed = run.TurnsUsed,
                Usage = AiUsageView.From(run.Usage)
            });
        }
    }
}
