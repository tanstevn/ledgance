using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;

namespace Ledgance.Audit.Engagement.Application.Engagements {
    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetEngagementsQuery : IQuery<Result<IEnumerable<EngagementListRow>>> {
        public Guid? ClientId { get; set; }
    }

    public class EngagementListRow {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public decimal BudgetHours { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GetEngagementsQueryHandler
        : IRequestHandler<GetEngagementsQuery, Result<IEnumerable<EngagementListRow>>> {
        private readonly IEngagementRepository _engagements;
        private readonly IClientLookup _clients;

        public GetEngagementsQueryHandler(IEngagementRepository engagements,
            IClientLookup clients) {
            _engagements = engagements;
            _clients = clients;
        }

        public async Task<Result<IEnumerable<EngagementListRow>>> HandleAsync(
            GetEngagementsQuery request, CancellationToken ct) {
            var engagements = await _engagements.ListAsync(request.ClientId, ct);
            var names = await _clients.GetNamesAsync(
                engagements.Select(e => e.ClientId).Distinct(), ct);

            return Result<IEnumerable<EngagementListRow>>.Success(engagements
                .Select(e => new EngagementListRow {
                    Id = e.Id,
                    ClientId = e.ClientId,
                    ClientName = names.GetValueOrDefault(e.ClientId, string.Empty),
                    Name = e.Name,
                    Type = e.Type.ToString(),
                    Status = e.Status.ToString(),
                    PeriodStart = e.PeriodStart,
                    PeriodEnd = e.PeriodEnd,
                    BudgetHours = e.BudgetHours,
                    CreatedAt = e.CreatedAt
                }));
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetEngagementByIdQuery : IQuery<Result<EngagementDetail>> {
        public Guid Id { get; set; }
    }

    public class EngagementDetail {
        public Guid Id { get; set; }
        public Guid ClientId { get; set; }
        public string ClientName { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateOnly PeriodStart { get; set; }
        public DateOnly PeriodEnd { get; set; }
        public DateOnly? FiscalYearEnd { get; set; }
        public decimal BudgetHours { get; set; }
        public DateTime CreatedAt { get; set; }
        public PlanView? Plan { get; set; }
        public MaterialityView? Materiality { get; set; }
        public List<TeamMemberView> Team { get; set; } = [];
        public ProgressView Progress { get; set; } = new();
    }

    public class PlanView {
        public string Scope { get; set; } = string.Empty;
        public string Objectives { get; set; } = string.Empty;
        public string Strategy { get; set; } = string.Empty;
        public DateOnly? TimelineStart { get; set; }
        public DateOnly? TimelineEnd { get; set; }
        public bool IsApproved { get; set; }
    }

    public class MaterialityView {
        public decimal OverallAmount { get; set; }
        public decimal PerformanceAmount { get; set; }
        public decimal ClearlyTrivialThreshold { get; set; }
        public string Basis { get; set; } = string.Empty;
        public string Rationale { get; set; } = string.Empty;
    }

    public class TeamMemberView {
        public Guid MemberId { get; set; }
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }

    public class ProgressView {
        public int OpenProcedures { get; set; }
        public int UnapprovedWorkingPapers { get; set; }
        public int OpenReviewNotes { get; set; }
        public int OpenFindings { get; set; }
        public int UnaddressedHighRisks { get; set; }
        public bool ReportFinalized { get; set; }
    }

    public class GetEngagementByIdQueryValidator : AbstractValidator<GetEngagementByIdQuery> {
        public GetEngagementByIdQueryValidator() {
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public class GetEngagementByIdQueryHandler
        : IRequestHandler<GetEngagementByIdQuery, Result<EngagementDetail>> {
        private readonly IEngagementRepository _engagements;
        private readonly ITeamRepository _team;
        private readonly IClientLookup _clients;
        private readonly IEngagementProgressReader _progress;
        private readonly IEngagementAccessGuard _access;
        private readonly IOrganizationDirectory _directory;
        private readonly ICurrentUserAccessor _currentUser;

        public GetEngagementByIdQueryHandler(IEngagementRepository engagements,
            ITeamRepository team, IClientLookup clients, IEngagementProgressReader progress,
            IEngagementAccessGuard access, IOrganizationDirectory directory,
            ICurrentUserAccessor currentUser) {
            _engagements = engagements;
            _team = team;
            _clients = clients;
            _progress = progress;
            _access = access;
            _directory = directory;
            _currentUser = currentUser;
        }

        public async Task<Result<EngagementDetail>> HandleAsync(GetEngagementByIdQuery request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.Id, ct);

            var engagement = await _engagements.FindAsync(request.Id, ct);

            if (engagement is null) {
                return Result<EngagementDetail>.Error("Engagement was not found.");
            }

            var team = await _team.ListAsync(engagement.Id, ct);
            var names = await _clients.GetNamesAsync([engagement.ClientId], ct);
            var progress = await _progress.GetAsync(engagement.Id, ct);
            var members = await _directory.ListMembersAsync(
                _currentUser.RequireOrganizationId(), ct);
            var membersByUser = members.ToDictionary(m => m.UserId);

            return Result<EngagementDetail>.Success(new EngagementDetail {
                Id = engagement.Id,
                ClientId = engagement.ClientId,
                ClientName = names.GetValueOrDefault(engagement.ClientId, string.Empty),
                Name = engagement.Name,
                Type = engagement.Type.ToString(),
                Status = engagement.Status.ToString(),
                PeriodStart = engagement.PeriodStart,
                PeriodEnd = engagement.PeriodEnd,
                FiscalYearEnd = engagement.FiscalYearEnd,
                BudgetHours = engagement.BudgetHours,
                CreatedAt = engagement.CreatedAt,
                Plan = engagement.Plan is null ? null : new PlanView {
                    Scope = engagement.Plan.Scope,
                    Objectives = engagement.Plan.Objectives,
                    Strategy = engagement.Plan.Strategy,
                    TimelineStart = engagement.Plan.TimelineStart,
                    TimelineEnd = engagement.Plan.TimelineEnd,
                    IsApproved = engagement.Plan.IsApproved
                },
                Materiality = engagement.Materiality is null ? null : new MaterialityView {
                    OverallAmount = engagement.Materiality.OverallAmount,
                    PerformanceAmount = engagement.Materiality.PerformanceAmount,
                    ClearlyTrivialThreshold = engagement.Materiality.ClearlyTrivialThreshold,
                    Basis = engagement.Materiality.Basis,
                    Rationale = engagement.Materiality.Rationale
                },
                Team = team.Select(member => new TeamMemberView {
                    MemberId = member.Id,
                    UserId = member.UserId,
                    DisplayName = membersByUser.TryGetValue(member.UserId, out var info)
                        ? info.DisplayName : string.Empty,
                    Email = membersByUser.TryGetValue(member.UserId, out var infoEmail)
                        ? infoEmail.Email : string.Empty,
                    Role = member.Role.ToString()
                }).ToList(),
                Progress = new ProgressView {
                    OpenProcedures = progress.OpenProcedures,
                    UnapprovedWorkingPapers = progress.UnapprovedWorkingPapers,
                    OpenReviewNotes = progress.OpenReviewNotes,
                    OpenFindings = progress.OpenFindings,
                    UnaddressedHighRisks = progress.UnaddressedHighRisks,
                    ReportFinalized = progress.ReportFinalized
                }
            });
        }
    }
}
