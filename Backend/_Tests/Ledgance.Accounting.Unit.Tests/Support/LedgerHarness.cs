using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.ChartOfAccounts;
using Ledgance.Accounting.Ledger.Application.Documents;
using Ledgance.Accounting.Ledger.Application.Entities;
using Ledgance.Accounting.Ledger.Application.Journal;
using Ledgance.Accounting.Ledger.Application.Ledger;
using Ledgance.Accounting.Ledger.Application.Periods;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Application.Reconciliations;
using Ledgance.Accounting.Ledger.Application.Reports;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Support {
    /// <summary>
    /// Wires every Ledger slice through the real mediator pipeline over in-memory fakes, so
    /// workflow tests exercise authorization, entitlements and validation as production does.
    /// </summary>
    public sealed class LedgerHarness {
        public LedgerHarness(CurrentUser? user) {
            Harness = new MediatorTestHarness(user)
                .WithService<IEntityRepository>(Entities)
                .WithService<IAccountRepository>(Accounts)
                .WithService<IFiscalPeriodRepository>(Periods)
                .WithService<IJournalEntryRepository>(Entries)
                .WithService<ILedgerLineRepository>(LedgerLines)
                .WithService<IReconciliationRepository>(Reconciliations)
                .WithService<IDocumentRepository>(Documents)
                .WithService<IDocumentFileStore>(FileStore)
                .WithService<IEntityGuard>(new EntityGuard(Entities))
                .WithService<IActivityRecorder>(Activity)
                .WithHandler<CreateEntityCommand, Result<Guid>, CreateEntityCommandHandler>()
                .WithHandler<UpdateEntityCommand, Result<bool>, UpdateEntityCommandHandler>()
                .WithHandler<ArchiveEntityCommand, Result<bool>, ArchiveEntityCommandHandler>()
                .WithHandler<GetEntitiesQuery, Result<IEnumerable<EntityRow>>,
                    GetEntitiesQueryHandler>()
                .WithHandler<CreateAccountCommand, Result<Guid>, CreateAccountCommandHandler>()
                .WithHandler<UpdateAccountCommand, Result<bool>, UpdateAccountCommandHandler>()
                .WithHandler<SetAccountActiveCommand, Result<bool>,
                    SetAccountActiveCommandHandler>()
                .WithHandler<GetChartOfAccountsQuery, Result<IEnumerable<AccountRow>>,
                    GetChartOfAccountsQueryHandler>()
                .WithHandler<CreateFiscalPeriodCommand, Result<Guid>,
                    CreateFiscalPeriodCommandHandler>()
                .WithHandler<CloseFiscalPeriodCommand, Result<bool>,
                    CloseFiscalPeriodCommandHandler>()
                .WithHandler<ReopenFiscalPeriodCommand, Result<bool>,
                    ReopenFiscalPeriodCommandHandler>()
                .WithHandler<CreateJournalEntryCommand, Result<Guid>,
                    CreateJournalEntryCommandHandler>()
                .WithHandler<UpdateJournalEntryCommand, Result<bool>,
                    UpdateJournalEntryCommandHandler>()
                .WithHandler<DeleteJournalEntryCommand, Result<bool>,
                    DeleteJournalEntryCommandHandler>()
                .WithHandler<PostJournalEntryCommand, Result<bool>,
                    PostJournalEntryCommandHandler>()
                .WithHandler<ReverseJournalEntryCommand, Result<Guid>,
                    ReverseJournalEntryCommandHandler>()
                .WithHandler<GetJournalEntriesQuery, PaginatedResult<JournalEntryRow>,
                    GetJournalEntriesQueryHandler>()
                .WithHandler<GetJournalEntryQuery, Result<JournalEntryDetail>,
                    GetJournalEntryQueryHandler>()
                .WithHandler<GetGeneralLedgerQuery, Result<GeneralLedgerView>,
                    GetGeneralLedgerQueryHandler>()
                .WithHandler<GetTrialBalanceQuery, Result<TrialBalanceView>,
                    GetTrialBalanceQueryHandler>()
                .WithHandler<GetIncomeStatementQuery, Result<IncomeStatementView>,
                    GetIncomeStatementQueryHandler>()
                .WithHandler<GetBalanceSheetQuery, Result<BalanceSheetView>,
                    GetBalanceSheetQueryHandler>()
                .WithHandler<StartReconciliationCommand, Result<Guid>,
                    StartReconciliationCommandHandler>()
                .WithHandler<SetClearedLinesCommand, Result<bool>,
                    SetClearedLinesCommandHandler>()
                .WithHandler<CompleteReconciliationCommand, Result<bool>,
                    CompleteReconciliationCommandHandler>()
                .WithHandler<CancelReconciliationCommand, Result<bool>,
                    CancelReconciliationCommandHandler>()
                .WithHandler<GetReconciliationsQuery, Result<IEnumerable<ReconciliationRow>>,
                    GetReconciliationsQueryHandler>()
                .WithHandler<GetReconciliationQuery, Result<ReconciliationDetail>,
                    GetReconciliationQueryHandler>()
                .WithHandler<UploadDocumentCommand, Result<Guid>, UploadDocumentCommandHandler>()
                .WithHandler<GetDocumentsQuery, Result<IEnumerable<DocumentRow>>,
                    GetDocumentsQueryHandler>()
                .WithHandler<GetDocumentDownloadUrlQuery, Result<string>,
                    GetDocumentDownloadUrlQueryHandler>()
                .WithValidator<CreateEntityCommand>(new CreateEntityCommandValidator())
                .WithValidator<CreateAccountCommand>(new CreateAccountCommandValidator())
                .WithValidator<CreateFiscalPeriodCommand>(
                    new CreateFiscalPeriodCommandValidator())
                .WithValidator<CreateJournalEntryCommand>(
                    new CreateJournalEntryCommandValidator())
                .WithValidator<UpdateJournalEntryCommand>(
                    new UpdateJournalEntryCommandValidator())
                .WithValidator<UploadDocumentCommand>(new UploadDocumentCommandValidator());
        }

        public MediatorTestHarness Harness { get; }
        public InMemoryEntityRepository Entities { get; } = new();
        public InMemoryAccountRepository Accounts { get; } = new();
        public InMemoryFiscalPeriodRepository Periods { get; } = new();
        public InMemoryJournalEntryRepository Entries { get; } = new();
        public InMemoryLedgerLineRepository LedgerLines { get; } = new();
        public InMemoryReconciliationRepository Reconciliations { get; } = new();
        public InMemoryDocumentRepository Documents { get; } = new();
        public FakeDocumentFileStore FileStore { get; } = new();
        public RecordingActivityRecorder Activity { get; } = new();

        public FakeEntitlementService Entitlements => Harness.Entitlements;

        public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request) =>
            Harness.SendAsync(request);
    }
}
