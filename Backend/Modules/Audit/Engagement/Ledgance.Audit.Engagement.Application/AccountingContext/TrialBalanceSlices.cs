using FluentValidation;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Authorization;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using System.Globalization;

namespace Ledgance.Audit.Engagement.Application.AccountingContext {
    /// <summary>
    /// The Audit-owned boundary for accounting context. External files are the baseline source;
    /// Phase 6 adds a Ledgance Accounting adapter behind this same abstraction. Audit never
    /// requires Ledgance Accounting.
    /// </summary>
    public interface IAccountingContextSource {
        TrialBalanceSource Source { get; }
        Task<IReadOnlyList<TrialBalanceLine>> ReadTrialBalanceAsync(string payload,
            CancellationToken ct);
    }

    /// <summary>
    /// Parses "AccountCode,AccountName,Debit,Credit" CSV, with an optional header row.
    /// </summary>
    public sealed class CsvAccountingContextSource : IAccountingContextSource {
        public TrialBalanceSource Source => TrialBalanceSource.ExternalCsv;

        public Task<IReadOnlyList<TrialBalanceLine>> ReadTrialBalanceAsync(string payload,
            CancellationToken ct) {
            var lines = new List<TrialBalanceLine>();

            foreach (var rawLine in payload.Split('\n')) {
                var line = rawLine.Trim().TrimEnd('\r');

                if (line.Length == 0) {
                    continue;
                }

                var cells = SplitCsvLine(line);

                if (cells.Count < 4) {
                    throw new DomainRuleException(
                        $"Each trial balance row needs 4 columns (code, name, debit, credit): '{line}'.");
                }

                var isHeader = lines.Count == 0
                    && !decimal.TryParse(cells[2], NumberStyles.Any,
                        CultureInfo.InvariantCulture, out _)
                    && !decimal.TryParse(cells[3], NumberStyles.Any,
                        CultureInfo.InvariantCulture, out _);

                if (isHeader) {
                    continue;
                }

                lines.Add(new TrialBalanceLine(
                    cells[0].Trim(),
                    cells[1].Trim(),
                    ParseAmount(cells[2], line),
                    ParseAmount(cells[3], line)));
            }

            return Task.FromResult<IReadOnlyList<TrialBalanceLine>>(lines);
        }

        private static decimal ParseAmount(string cell, string line) {
            var cleaned = cell.Trim().Replace(",", string.Empty);

            if (cleaned.Length == 0) {
                return 0m;
            }

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture,
                out var amount)
                ? amount
                : throw new DomainRuleException($"'{cell}' is not a valid amount in row '{line}'.");
        }

        private static List<string> SplitCsvLine(string line) {
            var cells = new List<string>();
            var current = new System.Text.StringBuilder();
            var inQuotes = false;

            foreach (var character in line) {
                if (character == '"') {
                    inQuotes = !inQuotes;
                }
                else if (character == ',' && !inQuotes) {
                    cells.Add(current.ToString());
                    current.Clear();
                }
                else {
                    current.Append(character);
                }
            }

            cells.Add(current.ToString());
            return cells;
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Contribute)]
    public class ImportTrialBalanceCommand : ICommand<Result<ImportTrialBalanceResult>> {
        public Guid EngagementId { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;
        public string CsvContent { get; set; } = string.Empty;
    }

    public class ImportTrialBalanceResult {
        public Guid ImportId { get; set; }
        public int LineCount { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsBalanced { get; set; }
    }

    public class ImportTrialBalanceCommandValidator : AbstractValidator<ImportTrialBalanceCommand> {
        public ImportTrialBalanceCommandValidator() {
            RuleFor(x => x.EngagementId).NotEmpty();
            RuleFor(x => x.PeriodLabel).NotEmpty().MaximumLength(50);
            RuleFor(x => x.CsvContent).NotEmpty();
        }
    }

    public class ImportTrialBalanceCommandHandler
        : IRequestHandler<ImportTrialBalanceCommand, Result<ImportTrialBalanceResult>> {
        private readonly ITrialBalanceRepository _trialBalances;
        private readonly IAccountingContextSource _source;
        private readonly IEngagementAccessGuard _access;
        private readonly ICurrentUserAccessor _currentUser;
        private readonly IActivityRecorder _activity;

        public ImportTrialBalanceCommandHandler(ITrialBalanceRepository trialBalances,
            IAccountingContextSource source, IEngagementAccessGuard access,
            ICurrentUserAccessor currentUser, IActivityRecorder activity) {
            _trialBalances = trialBalances;
            _source = source;
            _access = access;
            _currentUser = currentUser;
            _activity = activity;
        }

        public async Task<Result<ImportTrialBalanceResult>> HandleAsync(
            ImportTrialBalanceCommand request, CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var lines = await _source.ReadTrialBalanceAsync(request.CsvContent, ct);

            var import = TrialBalanceImport.Create(request.EngagementId, _source.Source,
                request.PeriodLabel, lines, _currentUser.Require().UserId);

            await _trialBalances.AddAsync(import, ct);

            await _activity.RecordAsync(new ActivityEntry("Audit", "trial_balance.imported",
                "TrialBalance", import.Id,
                $"A trial balance ({import.Lines.Count} lines, {(import.IsBalanced ? "balanced" : "OUT OF BALANCE")}) was imported.",
                request.EngagementId), ct);

            return Result<ImportTrialBalanceResult>.Success(new ImportTrialBalanceResult {
                ImportId = import.Id,
                LineCount = import.Lines.Count,
                TotalDebits = import.TotalDebits,
                TotalCredits = import.TotalCredits,
                IsBalanced = import.IsBalanced
            });
        }
    }

    [RequiresPermission(AuditEngagementPermissions.Read)]
    public class GetTrialBalanceQuery : IQuery<Result<TrialBalanceView>> {
        public Guid EngagementId { get; set; }
    }

    public class TrialBalanceView {
        public Guid ImportId { get; set; }
        public string Source { get; set; } = string.Empty;
        public string PeriodLabel { get; set; } = string.Empty;
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public bool IsBalanced { get; set; }
        public DateTime ImportedAt { get; set; }
        public List<TrialBalanceLineView> Lines { get; set; } = [];
    }

    public class TrialBalanceLineView {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class GetTrialBalanceQueryHandler
        : IRequestHandler<GetTrialBalanceQuery, Result<TrialBalanceView>> {
        private readonly ITrialBalanceRepository _trialBalances;
        private readonly IEngagementAccessGuard _access;

        public GetTrialBalanceQueryHandler(ITrialBalanceRepository trialBalances,
            IEngagementAccessGuard access) {
            _trialBalances = trialBalances;
            _access = access;
        }

        public async Task<Result<TrialBalanceView>> HandleAsync(GetTrialBalanceQuery request,
            CancellationToken ct) {
            await _access.EnsureMemberAsync(request.EngagementId, ct);

            var import = await _trialBalances.FindLatestAsync(request.EngagementId, ct);

            if (import is null) {
                return Result<TrialBalanceView>.Error(
                    "No trial balance has been imported for this engagement.");
            }

            return Result<TrialBalanceView>.Success(new TrialBalanceView {
                ImportId = import.Id,
                Source = import.Source.ToString(),
                PeriodLabel = import.PeriodLabel,
                TotalDebits = import.TotalDebits,
                TotalCredits = import.TotalCredits,
                IsBalanced = import.IsBalanced,
                ImportedAt = import.ImportedAt,
                Lines = import.Lines.Select(line => new TrialBalanceLineView {
                    AccountCode = line.AccountCode,
                    AccountName = line.AccountName,
                    Debit = line.Debit,
                    Credit = line.Credit
                }).ToList()
            });
        }
    }
}
