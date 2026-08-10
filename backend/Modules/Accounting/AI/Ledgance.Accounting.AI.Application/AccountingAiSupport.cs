using Ledgance.Accounting.AI.Domain;
using Ledgance.Accounting.Ledger.Application.Ports;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.Accounting.AI.Application {
    /// <summary>
    /// Every Accounting AI result is a proposal: attributed, reviewable, and never applied to
    /// the books until a human records it through the normal (authorized) commands.
    /// </summary>
    public class AiProposalResult {
        public string Capability { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;

        public string Disclaimer { get; set; } =
            "AI-generated proposal. It assists professional judgment and is not verified " +
            "accounting fact — review it before recording anything in the books.";

        public static AiProposalResult From(AccountingAiCapability capability,
            AiCompletion completion) =>
            new() {
                Capability = capability.Key,
                Content = completion.Content,
                Provider = completion.Provider,
                Model = completion.Model,
                Tier = completion.Tier
            };
    }

    internal static class AccountingAiPrompts {
        public const string SystemBase =
            "You are an AI assistant for accounting professionals working inside the " +
            "Ledgance Accounting platform, a double-entry bookkeeping system. Ground every " +
            "statement in the accounting context you are given; when the context does not " +
            "contain the answer, say so instead of guessing. Never present your output as " +
            "verified accounting fact — it is a proposal the accountant must review. Be " +
            "precise, structured and concise, and state amounts in the entity's base currency.";

        public static AiWorkload Workload(AccountingAiCapability capability, string instruction,
            string userPrompt, IReadOnlyList<AiDocument>? context = null) =>
            AiWorkload.For(ProductModule.Accounting, capability.Key, capability.RequiredTier,
                $"{SystemBase}\n\n{instruction}", userPrompt, context);
    }

    /// <summary>
    /// Builds AI context documents from ledger data the caller is already authorized to read.
    /// This is the only material AI sees — there is no privileged AI data path.
    /// </summary>
    internal static class LedgerAiContext {
        public static AiDocument EntityOverview(AccountingEntity entity,
            IReadOnlyList<FiscalPeriod> periods) {
            var periodLines = periods.Count == 0
                ? "No fiscal periods have been defined yet."
                : string.Join('\n', periods
                    .OrderBy(period => period.StartDate)
                    .Select(period => $"- [{period.Status}] {period.Name}: " +
                        $"{period.StartDate:yyyy-MM-dd} to {period.EndDate:yyyy-MM-dd}"));

            return new AiDocument("Entity overview",
                $"Entity: {entity.Name}" +
                (entity.LegalName.Length > 0 ? $" ({entity.LegalName})" : "") +
                $"\nBase currency: {entity.BaseCurrency}\n" +
                $"Archived: {entity.IsArchived}\n\nFiscal periods:\n{periodLines}");
        }

        public static AiDocument? ChartOfAccounts(IReadOnlyList<Account> accounts) {
            if (accounts.Count == 0) {
                return null;
            }

            var parents = accounts
                .Where(account => account.ParentAccountId is not null)
                .Select(account => account.ParentAccountId!.Value)
                .ToHashSet();

            var lines = accounts
                .OrderBy(account => account.Code, StringComparer.OrdinalIgnoreCase)
                .Select(account =>
                    $"{account.Code}\t{account.Name}\t{account.Type}" +
                    (account.Classification.Length > 0 ? $" ({account.Classification})" : "") +
                    (parents.Contains(account.Id) ? "\t[summary — not postable]" : "") +
                    (account.IsActive ? "" : "\t[inactive]"));

            return new AiDocument("Chart of accounts",
                "Code\tName\tType\n" + string.Join('\n', lines));
        }

        public static AiDocument? JournalEntries(IReadOnlyList<JournalEntry> entries) {
            if (entries.Count == 0) {
                return null;
            }

            var lines = entries
                .OrderBy(entry => entry.EntryNumber)
                .Select(entry => $"- [{entry.Status}] #{entry.EntryNumber} " +
                    $"{entry.EntryDate:yyyy-MM-dd} {entry.Memo}" +
                    (entry.Reference.Length > 0 ? $" (ref {entry.Reference})" : "") +
                    $" — {entry.TotalDebits:N2}");

            return new AiDocument("Journal entries", string.Join('\n', lines));
        }

        public static AiDocument EntryDetail(JournalEntry entry,
            IReadOnlyDictionary<Guid, Account> accounts) {
            var lines = entry.Lines.Select(line => {
                var account = accounts.GetValueOrDefault(line.AccountId);
                return $"{account?.Code ?? "?"}\t{account?.Name ?? "Unknown account"}\t" +
                    $"{line.Debit:N2}\t{line.Credit:N2}" +
                    (line.Description.Length > 0 ? $"\t{line.Description}" : "");
            });

            return new AiDocument(
                $"Journal entry #{entry.EntryNumber} ({entry.Status})",
                $"Date: {entry.EntryDate:yyyy-MM-dd}\nMemo: {entry.Memo}\n" +
                (entry.Reference.Length > 0 ? $"Reference: {entry.Reference}\n" : "") +
                "Account\tName\tDebit\tCredit\n" + string.Join('\n', lines) +
                $"\nTotals: {entry.TotalDebits:N2} / {entry.TotalCredits:N2}");
        }

        public static AiDocument? TrialBalance(FiscalPeriod period,
            IReadOnlyList<PostedLedgerLine> linesToPeriodEnd,
            IReadOnlyDictionary<Guid, Account> accounts) {
            if (linesToPeriodEnd.Count == 0) {
                return null;
            }

            var rows = linesToPeriodEnd
                .GroupBy(line => line.AccountId)
                .Select(group => {
                    var account = accounts.GetValueOrDefault(group.Key);
                    var debits = group.Sum(line => line.Debit);
                    var credits = group.Sum(line => line.Credit);
                    var net = debits - credits;

                    return new {
                        Code = account?.Code ?? "?",
                        Name = account?.Name ?? "Unknown account",
                        Type = account?.Type.ToString() ?? "?",
                        Debit = net > 0 ? net : 0,
                        Credit = net < 0 ? -net : 0
                    };
                })
                .OrderBy(row => row.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lines = rows.Select(row =>
                $"{row.Code}\t{row.Name}\t{row.Type}\t{row.Debit:N2}\t{row.Credit:N2}");

            return new AiDocument(
                $"Trial balance as of {period.EndDate:yyyy-MM-dd} ({period.Name})",
                "Code\tName\tType\tDebitBalance\tCreditBalance\n" + string.Join('\n', lines) +
                $"\nTotals: {rows.Sum(row => row.Debit):N2} / {rows.Sum(row => row.Credit):N2}");
        }

        public static AiDocument? PeriodStatements(FiscalPeriod period,
            IReadOnlyList<PostedLedgerLine> linesToPeriodEnd,
            IReadOnlyDictionary<Guid, Account> accounts) {
            if (linesToPeriodEnd.Count == 0) {
                return null;
            }

            var balances = linesToPeriodEnd
                .Where(line => accounts.ContainsKey(line.AccountId))
                .GroupBy(line => line.AccountId)
                .Select(group => new {
                    Account = accounts[group.Key],
                    Balance = Account.NaturalBalance(accounts[group.Key].Type,
                        group.Sum(line => line.Debit), group.Sum(line => line.Credit)),
                    PeriodBalance = Account.NaturalBalance(accounts[group.Key].Type,
                        group.Where(line => line.EntryDate >= period.StartDate)
                            .Sum(line => line.Debit),
                        group.Where(line => line.EntryDate >= period.StartDate)
                            .Sum(line => line.Credit))
                })
                .ToList();

            string Section(AccountType type, bool periodOnly) => string.Join('\n', balances
                .Where(balance => balance.Account.Type == type)
                .Where(balance => (periodOnly ? balance.PeriodBalance : balance.Balance) != 0)
                .OrderBy(balance => balance.Account.Code, StringComparer.OrdinalIgnoreCase)
                .Select(balance => $"  {balance.Account.Code} {balance.Account.Name}: " +
                    $"{(periodOnly ? balance.PeriodBalance : balance.Balance):N2}"));

            decimal Total(AccountType type, bool periodOnly) => balances
                .Where(balance => balance.Account.Type == type)
                .Sum(balance => periodOnly ? balance.PeriodBalance : balance.Balance);

            var revenue = Total(AccountType.Revenue, periodOnly: true);
            var expenses = Total(AccountType.Expense, periodOnly: true);
            var earningsToDate = Total(AccountType.Revenue, periodOnly: false)
                - Total(AccountType.Expense, periodOnly: false);

            return new AiDocument(
                $"Financial statements ({period.Name}, ending {period.EndDate:yyyy-MM-dd})",
                $"Income statement for the period:\nRevenue:\n{Section(AccountType.Revenue, true)}\n" +
                $"Expenses:\n{Section(AccountType.Expense, true)}\n" +
                $"Total revenue: {revenue:N2}\nTotal expenses: {expenses:N2}\n" +
                $"Net income: {revenue - expenses:N2}\n\n" +
                $"Balance sheet as of period end:\nAssets:\n{Section(AccountType.Asset, false)}\n" +
                $"Liabilities:\n{Section(AccountType.Liability, false)}\n" +
                $"Equity:\n{Section(AccountType.Equity, false)}\n" +
                $"Total assets: {Total(AccountType.Asset, false):N2}\n" +
                $"Total liabilities: {Total(AccountType.Liability, false):N2}\n" +
                $"Total equity: {Total(AccountType.Equity, false):N2}\n" +
                $"Current earnings (life to date): {earningsToDate:N2}");
        }

        public static AiDocument Reconciliation(Reconciliation reconciliation, Account account,
            IReadOnlyList<PostedLedgerLine> linesToStatementDate) {
            var cleared = reconciliation.ClearedLineIds.ToHashSet();

            var lines = linesToStatementDate
                .OrderBy(line => line.EntryDate)
                .ThenBy(line => line.EntryNumber)
                .Select(line => $"- [{(cleared.Contains(line.EntryId) ? "cleared" : "UNCLEARED")}] " +
                    $"#{line.EntryNumber} {line.EntryDate:yyyy-MM-dd} " +
                    $"debit {line.Debit:N2} / credit {line.Credit:N2}" +
                    (line.Description.Length > 0 ? $" — {line.Description}" : ""));

            var clearedBalance = linesToStatementDate
                .Where(line => cleared.Contains(line.EntryId))
                .Sum(line => Account.NaturalBalance(account.Type, line.Debit, line.Credit));

            return new AiDocument(
                $"Reconciliation of {account.Code} '{account.Name}' " +
                $"as of {reconciliation.StatementDate:yyyy-MM-dd}",
                $"Statement balance: {reconciliation.StatementBalance:N2}\n" +
                $"Cleared balance so far: {clearedBalance:N2}\n" +
                $"Working difference: {reconciliation.StatementBalance - clearedBalance:N2}\n" +
                $"Status: {reconciliation.Status}\n\nLedger lines up to the statement date:\n" +
                string.Join('\n', lines));
        }
    }
}
