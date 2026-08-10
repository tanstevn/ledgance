using FluentValidation;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.AccountingContext;
using Ledgance.Audit.Engagement.Application.Engagements;
using Ledgance.Audit.Engagement.Application.Evidence;
using Ledgance.Audit.Engagement.Application.Fieldwork;
using Ledgance.Audit.Engagement.Application.Findings;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Application.WorkingPapers;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;
using System.Text.Json;

namespace Ledgance.Audit.AI.Application.Agent {
    public class AgentStepView {
        public string Tool { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
    }

    public class AgentRunReport {
        public string Capability { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
        public List<AgentStepView> Steps { get; set; } = [];
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int TurnsUsed { get; set; }

        public string Disclaimer { get; set; } =
            "AI-generated analysis produced by an agent over authorized, read-only data. " +
            "It assists professional judgment and must be reviewed before any use.";

        public static AgentRunReport From(AuditAiCapability capability, AgentRunResult run) =>
            new() {
                Capability = capability.Key,
                Answer = run.Answer,
                Steps = run.Steps.Select(step => new AgentStepView {
                    Tool = step.Tool,
                    Arguments = step.ArgumentsJson,
                    Result = step.Result
                }).ToList(),
                Provider = run.Provider,
                Model = run.Model,
                TurnsUsed = run.TurnsUsed
            };
    }

    internal static class AgentToolJson {
        private static readonly JsonSerializerOptions Options = new() {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public static async Task<string> DispatchAsync<T>(IMediator mediator,
            IRequest<Result<T>> request, CancellationToken ct) {
            var result = await mediator.SendAsync(request, ct);

            return result.Successful
                ? JsonSerializer.Serialize(result.Data, Options)
                : "Error: " + string.Join("; ", result.Errors ?? ["The request failed."]);
        }

        public static Guid GetGuid(JsonElement arguments, string name) =>
            arguments.TryGetProperty(name, out var property)
                && Guid.TryParse(property.GetString(), out var value)
                ? value
                : Guid.Empty;

        public const string NoParameters = """{"type":"object","properties":{}}""";
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class RunAuditAgentCommand : ICommand<Result<AgentRunReport>> {
        public Guid EngagementId { get; set; }

        /// <summary>
        /// What the agent should investigate, e.g. "assess whether the identified risks are
        /// consistent with the trial balance and the findings raised so far".
        /// </summary>
        public string Goal { get; set; } = string.Empty;
    }

    public class RunAuditAgentCommandValidator : AbstractValidator<RunAuditAgentCommand> {
        public RunAuditAgentCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Goal).NotEmpty().MaximumLength(4000);
        }
    }

    public class RunAuditAgentCommandHandler
        : IRequestHandler<RunAuditAgentCommand, Result<AgentRunReport>> {
        private const string SystemPrompt =
            "You are an investigative AI agent for a professional audit team inside the " +
            "Ledgance Audit platform. Work strictly from what your tools return — they are " +
            "your only source of engagement data, and they enforce the caller's " +
            "authorization. When a tool denies access or returns an error, respect it and " +
            "work with what you have. Investigate step by step, then give a structured, " +
            "sourced answer that distinguishes observation from interpretation. Your output " +
            "is a proposal for the engagement team, never a conclusion of record.";

        private readonly IAgentRunner _agent;
        private readonly IMediator _mediator;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public RunAuditAgentCommandHandler(IAgentRunner agent, IMediator mediator,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _agent = agent;
            _mediator = mediator;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<AgentRunReport>> HandleAsync(RunAuditAgentCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var run = await _agent.RunAsync(new AgentWorkload(ProductModule.Audit,
                AuditAiCapabilities.Agent.Key, request.Goal, SystemPrompt,
                BuildTools(request.EngagementId)), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.agent",
                "Engagement", request.EngagementId,
                $"An AI agent investigation ran ({run.Steps.Count} tool steps): {Trim(request.Goal)}",
                request.EngagementId), ct);

            return Result<AgentRunReport>.Success(
                AgentRunReport.From(AuditAiCapabilities.Agent, run));
        }

        private static string Trim(string goal) =>
            goal.Length <= 120 ? goal : goal[..120] + "…";

        /// <summary>
        /// The agent's whole world: read-only queries of this engagement, dispatched through
        /// the mediator so each call re-runs the full pipeline as the calling user. The
        /// engagement id is fixed here — the agent can never choose another engagement.
        /// </summary>
        private List<AgentTool> BuildTools(Guid engagementId) => [
            new AgentTool("get_engagement_overview",
                "The engagement's status, period, materiality, plan and progress.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetEngagementByIdQuery { Id = engagementId }, ct)),

            new AgentTool("list_risks",
                "The identified risks of material misstatement with responses.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetRisksQuery { EngagementId = engagementId }, ct)),

            new AgentTool("list_procedures",
                "The audit procedures with status and conclusions.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetProceduresQuery { EngagementId = engagementId }, ct)),

            new AgentTool("list_working_papers",
                "The working papers with status and open review notes.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetWorkingPapersQuery { EngagementId = engagementId }, ct)),

            new AgentTool("get_working_paper",
                "One working paper's full content and review notes.",
                """{"type":"object","properties":{"workingPaperId":{"type":"string","description":"The working paper id from list_working_papers."}},"required":["workingPaperId"]}""",
                (arguments, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetWorkingPaperByIdQuery {
                        EngagementId = engagementId,
                        WorkingPaperId = AgentToolJson.GetGuid(arguments, "workingPaperId")
                    }, ct)),

            new AgentTool("list_findings",
                "The findings raised so far with severity, status and recommendations.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetFindingsQuery { EngagementId = engagementId }, ct)),

            new AgentTool("list_evidence",
                "The evidence items with file metadata, versions and descriptions.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetEvidenceQuery { EngagementId = engagementId }, ct)),

            new AgentTool("get_trial_balance",
                "The trial balance imported into this engagement, with line detail.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetTrialBalanceQuery { EngagementId = engagementId }, ct)),

            new AgentTool("get_linked_accounting_context",
                "The organization's own Ledgance Accounting books (entities and fiscal " +
                "periods), when the organization has authorized sharing. Availability is " +
                "enforced server-side.",
                AgentToolJson.NoParameters,
                (_, ct) => AgentToolJson.DispatchAsync(_mediator,
                    new GetLinkedAccountingContextQuery(), ct))
        ];
    }
}
