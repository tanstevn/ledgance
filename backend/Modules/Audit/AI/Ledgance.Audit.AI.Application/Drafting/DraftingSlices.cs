using FluentValidation;
using Ledgance.Audit.AI.Domain;
using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Audit.AI.Application.Drafting {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class SuggestRisksCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public string? FocusArea { get; set; }
    }

    public class SuggestRisksCommandValidator : AbstractValidator<SuggestRisksCommand> {
        public SuggestRisksCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.FocusArea).MaximumLength(200);
        }
    }

    public class SuggestRisksCommandHandler
        : IRequestHandler<SuggestRisksCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IRiskRepository _risks;
        private readonly ITrialBalanceRepository _trialBalances;
        private readonly IActivityRecorder _activity;

        public SuggestRisksCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IRiskRepository risks,
            ITrialBalanceRepository trialBalances, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _risks = risks;
            _trialBalances = trialBalances;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(SuggestRisksCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                _engagements, _clients, ct);

            if (await EngagementAiContext.RisksAsync(request.EngagementId, _risks, ct)
                is { } risks) {
                context.Add(risks);
            }

            if (await EngagementAiContext.TrialBalanceAsync(request.EngagementId,
                    _trialBalances, ct) is { } trialBalance) {
                context.Add(trialBalance);
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.RiskSuggestions,
                "Suggest risks of material misstatement for this engagement that are not " +
                "already identified. For each suggestion give: title, description, affected " +
                "assertions, suggested likelihood and impact (Low/Medium/High), and a " +
                "suggested response. Do not repeat already-identified risks.",
                request.FocusArea is null
                    ? "Suggest additional risks for this engagement."
                    : $"Suggest additional risks, focusing on: {request.FocusArea}",
                context), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.risk_suggestions",
                "Engagement", request.EngagementId, "generated AI risk suggestions.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.RiskSuggestions, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class SuggestProceduresCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
    }

    public class SuggestProceduresCommandValidator : AbstractValidator<SuggestProceduresCommand> {
        public SuggestProceduresCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
        }
    }

    public class SuggestProceduresCommandHandler
        : IRequestHandler<SuggestProceduresCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IRiskRepository _risks;
        private readonly IProcedureRepository _procedures;
        private readonly IActivityRecorder _activity;

        public SuggestProceduresCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IRiskRepository risks, IProcedureRepository procedures,
            IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _risks = risks;
            _procedures = procedures;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            SuggestProceduresCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                _engagements, _clients, ct);

            if (await EngagementAiContext.RisksAsync(request.EngagementId, _risks, ct)
                is { } risks) {
                context.Add(risks);
            }

            if (await EngagementAiContext.ProceduresAsync(request.EngagementId,
                    _procedures, ct) is { } procedures) {
                context.Add(procedures);
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.ProcedureSuggestions,
                "Suggest audit procedures responsive to the identified risks, prioritizing " +
                "risks with no responsive procedure yet. For each suggestion give: area, " +
                "title, description of the work, and the risk(s) it responds to.",
                "Suggest audit procedures for this engagement.", context), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.procedure_suggestions",
                "Engagement", request.EngagementId, "generated AI procedure suggestions.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.ProcedureSuggestions, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class DraftWorkingPaperCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public Guid? ProcedureId { get; set; }
        public string Topic { get; set; } = string.Empty;
    }

    public class DraftWorkingPaperCommandValidator : AbstractValidator<DraftWorkingPaperCommand> {
        public DraftWorkingPaperCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Topic).NotEmpty().MaximumLength(500);
        }
    }

    public class DraftWorkingPaperCommandHandler
        : IRequestHandler<DraftWorkingPaperCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IProcedureRepository _procedures;
        private readonly IActivityRecorder _activity;

        public DraftWorkingPaperCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IProcedureRepository procedures,
            IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _procedures = procedures;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(
            DraftWorkingPaperCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                _engagements, _clients, ct);

            if (request.ProcedureId is not null) {
                var procedure = await _procedures.FindAsync(request.ProcedureId.Value, ct);

                if (procedure is null || procedure.EngagementId != request.EngagementId) {
                    return Result<AiProposalResult>.Error("Procedure was not found.");
                }

                context.Add(new AiDocument($"Procedure: {procedure.Title}",
                    $"Area: {procedure.Area}\nDescription: {procedure.Description}\n" +
                    $"Status: {procedure.Status}\nConclusion: {procedure.Conclusion}"));
            }

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.WorkingPaperDraft,
                "Draft working-paper content with these sections: Objective, Work performed, " +
                "Results, Conclusion. Mark every place requiring auditor input or evidence " +
                "references with [TODO].",
                $"Draft a working paper on: {request.Topic}", context), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.working_paper_draft",
                "Engagement", request.EngagementId, "generated an AI working-paper draft.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.WorkingPaperDraft, completion));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    [RequiresEntitlement(ProductModule.Audit, Entitlements.AiEnabled)]
    public class DraftFindingCommand : ICommand<Result<AiProposalResult>> {
        public Guid EngagementId { get; set; }
        public string Observation { get; set; } = string.Empty;
    }

    public class DraftFindingCommandValidator : AbstractValidator<DraftFindingCommand> {
        public DraftFindingCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Observation).NotEmpty().MaximumLength(4000);
        }
    }

    public class DraftFindingCommandHandler
        : IRequestHandler<DraftFindingCommand, Result<AiProposalResult>> {
        private readonly IAiCompletionService _ai;
        private readonly IEngagementAccessGuard _access;
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;
        private readonly IActivityRecorder _activity;

        public DraftFindingCommandHandler(IAiCompletionService ai,
            IEngagementAccessGuard access, IEngagementRepository engagements,
            IClientLookup clients, IActivityRecorder activity) {
            _ai = ai;
            _access = access;
            _engagements = engagements;
            _clients = clients;
            _activity = activity;
        }

        public async Task<Result<AiProposalResult>> HandleAsync(DraftFindingCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var context = await EngagementAiContext.OverviewAsync(request.EngagementId,
                _engagements, _clients, ct);

            var completion = await _ai.CompleteAsync(AuditAiPrompts.Workload(
                AuditAiCapabilities.FindingDraft,
                "Draft an audit finding from the observation: title, condition, criteria, " +
                "cause, effect, suggested severity (Low/Medium/High/Critical) and a " +
                "recommendation. Note what evidence should be attached.",
                $"Draft a finding from this observation: {request.Observation}", context), ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "ai.finding_draft",
                "Engagement", request.EngagementId, "generated an AI finding draft.",
                request.EngagementId), ct);

            return Result<AiProposalResult>.Success(
                AiProposalResult.From(AuditAiCapabilities.FindingDraft, completion));
        }
    }
}
