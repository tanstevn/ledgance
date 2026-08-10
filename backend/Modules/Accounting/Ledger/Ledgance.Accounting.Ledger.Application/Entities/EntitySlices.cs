using FluentValidation;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.Ledger.Application.Entities {
    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class CreateEntityCommand : ICommand<Result<Guid>> {
        public string Name { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string BaseCurrency { get; set; } = string.Empty;
    }

    public class CreateEntityCommandValidator : AbstractValidator<CreateEntityCommand> {
        public CreateEntityCommandValidator() {
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.LegalName).MaximumLength(200);
            RuleFor(x => x.BaseCurrency).NotEmpty().Length(3);
        }
    }

    public class CreateEntityCommandHandler : IRequestHandler<CreateEntityCommand, Result<Guid>> {
        private readonly IEntityRepository _entities;
        private readonly IEntitlementService _entitlements;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public CreateEntityCommandHandler(IEntityRepository entities,
            IEntitlementService entitlements, ICurrentUserAccessor currentUser,
            IActivityRecorder activity) {
            _entities = entities;
            _entitlements = entitlements;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<Guid>> HandleAsync(CreateEntityCommand request,
            CancellationToken ct) {
            var entitlements = await _entitlements.GetAsync(
                _currentUser.Require().OrganizationId, ProductModule.Accounting, ct);

            var activeEntities = await _entities.CountActiveAsync(ct);
            entitlements.RequireWithinLimit(Entitlements.MaxEntities, activeEntities + 1);

            var entity = AccountingEntity.Create(request.Name, request.LegalName,
                request.BaseCurrency);

            await _entities.AddAsync(entity, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "entity.created",
                "Entity", entity.Id, $"Entity '{entity.Name}' was created.", entity.Id), ct);

            return Result<Guid>.Success(entity.Id);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class UpdateEntityCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
    }

    public class UpdateEntityCommandValidator : AbstractValidator<UpdateEntityCommand> {
        public UpdateEntityCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
            RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
            RuleFor(x => x.LegalName).MaximumLength(200);
        }
    }

    public class UpdateEntityCommandHandler : IRequestHandler<UpdateEntityCommand, Result<bool>> {
        private readonly IEntityRepository _entities;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public UpdateEntityCommandHandler(IEntityRepository entities, IEntityGuard guard,
            IActivityRecorder activity) {
            _entities = entities;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(UpdateEntityCommand request,
            CancellationToken ct) {
            var entity = await _guard.RequireAsync(request.EntityId, ct);

            entity.Update(request.Name, request.LegalName);
            await _entities.UpdateAsync(entity, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "entity.updated",
                "Entity", entity.Id, $"Entity '{entity.Name}' was updated.", entity.Id), ct);

            return Result<bool>.Success(true);
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Manage)]
    public class ArchiveEntityCommand : ICommand<Result<bool>> {
        public Guid EntityId { get; set; }
    }

    public class ArchiveEntityCommandValidator : AbstractValidator<ArchiveEntityCommand> {
        public ArchiveEntityCommandValidator() {
            RuleFor(x => x.EntityId).NotEmpty();
        }
    }

    public class ArchiveEntityCommandHandler
        : IRequestHandler<ArchiveEntityCommand, Result<bool>> {
        private readonly IEntityRepository _entities;
        private readonly IFiscalPeriodRepository _periods;
        private readonly IEntityGuard _guard;
        private readonly IActivityRecorder _activity;

        public ArchiveEntityCommandHandler(IEntityRepository entities,
            IFiscalPeriodRepository periods, IEntityGuard guard, IActivityRecorder activity) {
            _entities = entities;
            _periods = periods;
            _guard = guard;
            _activity = activity;
        }

        public async Task<Result<bool>> HandleAsync(ArchiveEntityCommand request,
            CancellationToken ct) {
            var entity = await _guard.RequireAsync(request.EntityId, ct);
            var hasOpenPeriods = await _periods.AnyOpenAsync(entity.Id, ct);

            entity.Archive(hasOpenPeriods);
            await _entities.UpdateAsync(entity, ct);

            await _activity.RecordAsync(new ActivityEntry("Accounting", "entity.archived",
                "Entity", entity.Id, $"Entity '{entity.Name}' was archived.", entity.Id), ct);

            return Result<bool>.Success(true);
        }
    }

    public class EntityRow {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string LegalName { get; set; } = string.Empty;
        public string BaseCurrency { get; set; } = string.Empty;
        public bool IsArchived { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetEntitiesQuery : IQuery<Result<IEnumerable<EntityRow>>> { }

    public class GetEntitiesQueryHandler
        : IRequestHandler<GetEntitiesQuery, Result<IEnumerable<EntityRow>>> {
        private readonly IEntityRepository _entities;

        public GetEntitiesQueryHandler(IEntityRepository entities) {
            _entities = entities;
        }

        public async Task<Result<IEnumerable<EntityRow>>> HandleAsync(GetEntitiesQuery request,
            CancellationToken ct) {
            var entities = await _entities.ListAsync(ct);

            return Result<IEnumerable<EntityRow>>.Success(entities
                .Select(entity => new EntityRow {
                    Id = entity.Id,
                    Name = entity.Name,
                    LegalName = entity.LegalName,
                    BaseCurrency = entity.BaseCurrency,
                    IsArchived = entity.IsArchived,
                    CreatedAt = entity.CreatedAt
                }));
        }
    }

    [RequiresPermission(AccountingLedgerPermissions.Read)]
    public class GetEntityQuery : IQuery<Result<EntityRow>> {
        public Guid EntityId { get; set; }
    }

    public class GetEntityQueryHandler : IRequestHandler<GetEntityQuery, Result<EntityRow>> {
        private readonly IEntityRepository _entities;

        public GetEntityQueryHandler(IEntityRepository entities) {
            _entities = entities;
        }

        public async Task<Result<EntityRow>> HandleAsync(GetEntityQuery request,
            CancellationToken ct) {
            var entity = await _entities.FindAsync(request.EntityId, ct);

            if (entity is null) {
                return Result<EntityRow>.Error("The accounting entity was not found.");
            }

            return Result<EntityRow>.Success(new EntityRow {
                Id = entity.Id,
                Name = entity.Name,
                LegalName = entity.LegalName,
                BaseCurrency = entity.BaseCurrency,
                IsArchived = entity.IsArchived,
                CreatedAt = entity.CreatedAt
            });
        }
    }
}
