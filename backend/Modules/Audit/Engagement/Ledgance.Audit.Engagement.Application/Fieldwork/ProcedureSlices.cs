using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Fieldwork {
    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class AddProcedureCommand : ICommand<Result<Guid>> {
        public Guid EngagementId { get; set; }
        public string Area { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Guid> RiskIds { get; set; } = [];
        public Guid? AssigneeUserId { get; set; }
    }

    public class AddProcedureCommandValidator : AbstractValidator<AddProcedureCommand> {
        public AddProcedureCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.Area).MaximumLength(100);
        }
    }

    public class AddProcedureCommandHandler : IRequestHandler<AddProcedureCommand, Result<Guid>> {
        private readonly IProcedureRepository _procedures;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public AddProcedureCommandHandler(IProcedureRepository procedures,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _procedures = procedures;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(AddProcedureCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var procedure = AuditProcedure.Plan(request.EngagementId, request.Area,
                request.Title, request.Description, request.RiskIds, request.AssigneeUserId);

            await _procedures.AddAsync(procedure, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "procedure.planned",
                "Procedure", procedure.Id, $"planned the procedure {procedure.Title}.",
                request.EngagementId), ct);

            return Result<Guid>.Success(procedure.Id);
        }
    }

    public enum ProcedureAction { Start, Complete, MarkNotApplicable, Assign }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class UpdateProcedureCommand : ICommand<Result<string>> {
        public Guid EngagementId { get; set; }
        public Guid ProcedureId { get; set; }
        public ProcedureAction Action { get; set; }
        public string? Conclusion { get; set; }
        public Guid? AssigneeUserId { get; set; }
    }

    public class UpdateProcedureCommandValidator : AbstractValidator<UpdateProcedureCommand> {
        public UpdateProcedureCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.ProcedureId).NotEmpty();
            RuleFor(x => x.Action).IsInEnum();
        }
    }

    public class UpdateProcedureCommandHandler
        : IRequestHandler<UpdateProcedureCommand, Result<string>> {
        private readonly IProcedureRepository _procedures;
        private readonly IEngagementAccessGuard _access;
        private readonly IActivityRecorder _activity;

        public UpdateProcedureCommandHandler(IProcedureRepository procedures,
            IEngagementAccessGuard access, IActivityRecorder activity) {
            _procedures = procedures;
            _access = access;
            _activity = activity;
        }

        public async Task<Result<string>> HandleAsync(UpdateProcedureCommand request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var procedure = await _procedures.FindAsync(request.ProcedureId, ct);

            if (procedure is null || procedure.EngagementId != request.EngagementId) {
                return Result<string>.Error("Procedure was not found.");
            }

            switch (request.Action) {
                case ProcedureAction.Start:
                    procedure.Start();
                    break;
                case ProcedureAction.Complete:
                    procedure.Complete(request.Conclusion ?? string.Empty);
                    break;
                case ProcedureAction.MarkNotApplicable:
                    procedure.MarkNotApplicable(request.Conclusion ?? string.Empty);
                    break;
                case ProcedureAction.Assign:
                    procedure.Assign(request.AssigneeUserId);
                    break;
            }

            await _procedures.UpdateAsync(procedure, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "procedure.updated",
                "Procedure", procedure.Id,
                $"marked the procedure {procedure.Title} as {procedure.Status}.",
                request.EngagementId), ct);

            return Result<string>.Success(procedure.Status.ToString());
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetProceduresQuery : IQuery<Result<IEnumerable<ProcedureRow>>> {
        public Guid EngagementId { get; set; }
    }

    public class ProcedureRow {
        public Guid Id { get; set; }
        public string Area { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public List<Guid> RiskIds { get; set; } = [];
        public Guid? AssigneeUserId { get; set; }
        public string? Conclusion { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class GetProceduresQueryHandler
        : IRequestHandler<GetProceduresQuery, Result<IEnumerable<ProcedureRow>>> {
        private readonly IProcedureRepository _procedures;
        private readonly IEngagementAccessGuard _access;

        public GetProceduresQueryHandler(IProcedureRepository procedures,
            IEngagementAccessGuard access) {
            _procedures = procedures;
            _access = access;
        }

        public async Task<Result<IEnumerable<ProcedureRow>>> HandleAsync(
            GetProceduresQuery request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var procedures = await _procedures.ListAsync(request.EngagementId, ct);

            return Result<IEnumerable<ProcedureRow>>.Success(procedures
                .Select(procedure => new ProcedureRow {
                    Id = procedure.Id,
                    Area = procedure.Area,
                    Title = procedure.Title,
                    Description = procedure.Description,
                    Status = procedure.Status.ToString(),
                    RiskIds = procedure.RiskIds,
                    AssigneeUserId = procedure.AssigneeUserId,
                    Conclusion = procedure.Conclusion,
                    CompletedAt = procedure.CompletedAt
                }));
        }
    }
}
