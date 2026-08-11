using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Fieldwork {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class AddRiskCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Assertions { get; set; } = string.Empty;
        public RiskRating Likelihood { get; set; }
        public RiskRating Impact { get; set; }
        public string PlannedResponse { get; set; } = string.Empty;
    }

    public class AddRiskCommandValidator : AbstractValidator<AddRiskCommand> {
        public AddRiskCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Likelihood).IsInEnum();
            RuleFor(x => x.Impact).IsInEnum();
        }
    }

    public class AddRiskCommandHandler : IRequestHandler<AddRiskCommand, Result<Guid>> {
        private readonly IRiskRepository _risks;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public AddRiskCommandHandler(IRiskRepository risks, IEngagementAccessGuard access,
            IActivityRecorder activity) {
            _risks = risks;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(AddRiskCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var risk = Risk.Identify(request.EngagementId, request.Title, request.Description,
                request.Assertions, request.Likelihood, request.Impact, request.PlannedResponse);

            await _risks.AddAsync(risk, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "risk.identified",
                "Risk", risk.Id, $"identified the risk {risk.Title}, rated {risk.Level}.",
                request.EngagementId), ct);

            return Result<Guid>.Success(risk.Id);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class UpdateRiskCommand : ICommand<Result<bool>> {
        public Guid EngagementId { get; set; }
        public Guid RiskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Assertions { get; set; } = string.Empty;
        public RiskRating Likelihood { get; set; }
        public RiskRating Impact { get; set; }
        public string PlannedResponse { get; set; } = string.Empty;
    }

    public class UpdateRiskCommandValidator : AbstractValidator<UpdateRiskCommand> {
        public UpdateRiskCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.RiskId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        }
    }

    public class UpdateRiskCommandHandler : IRequestHandler<UpdateRiskCommand, Result<bool>> {
        private readonly IRiskRepository _risks;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public UpdateRiskCommandHandler(IRiskRepository risks, IEngagementAccessGuard access,
            IActivityRecorder activity) {
            _risks = risks;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateRiskCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var risk = await _risks.FindAsync(request.RiskId, ct);

            if (risk is null || risk.EngagementId != request.EngagementId) {
                return Result<bool>.Error("Risk was not found.");
            }

            risk.Reassess(request.Title, request.Description, request.Assertions,
                request.Likelihood, request.Impact, request.PlannedResponse);

            await _risks.UpdateAsync(risk, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "risk.reassessed",
                "Risk", risk.Id, $"reassessed the risk {risk.Title} as {risk.Level}.",
                request.EngagementId), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetRisksQuery : IQuery<Result<IEnumerable<RiskRow>>> {
        public Guid EngagementId { get; set; }
    }

    public class RiskRow {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Assertions { get; set; } = string.Empty;
        public string Likelihood { get; set; } = string.Empty;
        public string Impact { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string PlannedResponse { get; set; } = string.Empty;
        public int LinkedProcedures { get; set; }
    }

    public class GetRisksQueryHandler : IRequestHandler<GetRisksQuery, Result<IEnumerable<RiskRow>>> {
        private readonly IRiskRepository _risks;
        private readonly IProcedureRepository _procedures;
        private readonly IEngagementAccessGuard _access;

        public GetRisksQueryHandler(IRiskRepository risks, IProcedureRepository procedures,
            IEngagementAccessGuard access) {
            _risks = risks;
            _procedures = procedures;
            _access = access;
        }

        public async Task<Result<IEnumerable<RiskRow>>> HandleAsync(GetRisksQuery request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var risks = await _risks.ListAsync(request.EngagementId, ct);
            var procedures = await _procedures.ListAsync(request.EngagementId, ct);

            return Result<IEnumerable<RiskRow>>.Success(risks
                .Select(risk => new RiskRow {
                    Id = risk.Id,
                    Title = risk.Title,
                    Description = risk.Description,
                    Assertions = risk.Assertions,
                    Likelihood = risk.Likelihood.ToString(),
                    Impact = risk.Impact.ToString(),
                    Level = risk.Level.ToString(),
                    PlannedResponse = risk.PlannedResponse,
                    LinkedProcedures = procedures.Count(p => p.RiskIds.Contains(risk.Id))
                }));
        }
    }
}
