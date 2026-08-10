using Ledgance.Accounting.Ledger.Application;
using Ledgance.Accounting.Ledger.Application.Journal;
using Ledgance.Accounting.Ledger.Application.Periods;
using Ledgance.Accounting.Ledger.Domain;
using Ledgance.Accounting.Unit.Tests.Support;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.TestInfrastructure;

namespace Ledgance.Accounting.Unit.Tests.Workflows {
    public class JournalWorkflowTests {
        private static readonly DateOnly March15 = new(2026, 3, 15);

        private static LedgerHarness Harness(OrganizationRole role = OrganizationRole.Manager,
            params string[] permissions) {
            var granted = permissions.Length > 0
                ? permissions
                : [AccountingLedgerPermissions.Read, AccountingLedgerPermissions.Contribute,
                    AccountingLedgerPermissions.Manage];

            return new LedgerHarness(TestIdentity.User(role, permissions: granted));
        }

        private static (AccountingEntity Entity, Account Cash, Account Revenue) Seed(
            LedgerHarness harness, bool openPeriod = true) {
            var entity = AccountingEntity.Create("Acme", "", "PHP");
            harness.Entities.Entities.Add(entity);

            var period = FiscalPeriod.Open(entity.Id, "March 2026",
                new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31));

            if (!openPeriod) {
                period.Close(hasDraftEntries: false, Guid.NewGuid());
            }

            harness.Periods.Periods.Add(period);

            var cash = Account.Open(entity.Id, "1010", "Cash", AccountType.Asset, "", null);
            var revenue = Account.Open(entity.Id, "4010", "Sales", AccountType.Revenue, "",
                null);
            harness.Accounts.Accounts.AddRange([cash, revenue]);

            return (entity, cash, revenue);
        }

        private static CreateJournalEntryCommand SaleEntry(Guid entityId, Guid cashId,
            Guid revenueId, decimal amount = 500m, DateOnly? date = null) =>
            new() {
                EntityId = entityId,
                EntryDate = date ?? March15,
                Memo = "Cash sale",
                Reference = "INV-001",
                Lines = [
                    new JournalLineInput(cashId, "Cash received", amount, 0),
                    new JournalLineInput(revenueId, "Sales revenue", 0, amount)
                ]
            };

        [Fact]
        public async Task Drafting_and_posting_an_entry_writes_ledger_lines_and_activity() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            var created = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));
            Assert.True(created.Successful);

            var posted = await harness.SendAsync(new PostJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = created.Data
            });
            Assert.True(posted.Successful);

            Assert.Equal(2, harness.LedgerLines.Lines.Count);
            Assert.Equal(JournalEntryStatus.Posted,
                harness.Entries.Entries.Single().Status);
            Assert.Contains(harness.Activity.Entries,
                entry => entry.Action == "journal.drafted");
            Assert.Contains(harness.Activity.Entries,
                entry => entry.Action == "journal.posted");
        }

        [Fact]
        public async Task A_viewer_cannot_draft_a_journal_entry() {
            var harness = Harness(OrganizationRole.Viewer,
                AccountingLedgerPermissions.Read);
            var (entity, cash, revenue) = Seed(harness);

            await Assert.ThrowsAsync<ForbiddenException>(
                () => harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id)));
        }

        [Fact]
        public async Task An_unbalanced_entry_is_rejected_by_the_domain() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            var command = SaleEntry(entity.Id, cash.Id, revenue.Id);
            command.Lines[1] = new JournalLineInput(revenue.Id, "Short", 0, 400m);

            await Assert.ThrowsAsync<DomainRuleException>(
                () => harness.SendAsync(command));
        }

        [Fact]
        public async Task An_entry_dated_outside_every_period_is_rejected() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            var result = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id,
                date: new DateOnly(2026, 5, 1)));

            Assert.False(result.Successful);
            Assert.Contains("No fiscal period", result.Errors!.Single());
        }

        [Fact]
        public async Task An_entry_dated_in_a_closed_period_is_rejected() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness, openPeriod: false);

            var result = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));

            Assert.False(result.Successful);
            Assert.Contains("closed", result.Errors!.Single());
        }

        [Fact]
        public async Task Posting_to_a_summary_account_is_rejected() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            var child = Account.Open(entity.Id, "1011", "Petty cash", AccountType.Asset, "",
                cash);
            harness.Accounts.Accounts.Add(child);

            var result = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));

            Assert.False(result.Successful);
            Assert.Contains("summary account", result.Errors!.Single());
        }

        [Fact]
        public async Task Posting_to_an_inactive_account_is_rejected() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);
            revenue.Deactivate();

            var result = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));

            Assert.False(result.Successful);
            Assert.Contains("inactive", result.Errors!.Single());
        }

        [Fact]
        public async Task The_per_period_transaction_limit_is_enforced() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            harness.Entitlements.With(ProductModule.Accounting, PlanCode.Free,
                new Dictionary<string, string> {
                    [Entitlements.MaxTransactionsPerPeriod] = "1"
                });

            var first = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));
            Assert.True(first.Successful);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id)));

            Assert.Contains(Entitlements.MaxTransactionsPerPeriod, exception.Message);
        }

        [Fact]
        public async Task A_member_cannot_reverse_an_entry_but_a_manager_can() {
            var member = Harness(OrganizationRole.Member,
                AccountingLedgerPermissions.Read, AccountingLedgerPermissions.Contribute);
            var (entity, cash, revenue) = Seed(member);

            var created = await member.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));
            await member.SendAsync(new PostJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = created.Data
            });

            await Assert.ThrowsAsync<ForbiddenException>(
                () => member.SendAsync(new ReverseJournalEntryCommand {
                    EntityId = entity.Id,
                    EntryId = created.Data,
                    ReversalDate = March15
                }));

            var manager = Harness();
            manager.Entities.Entities.AddRange(member.Entities.Entities);
            manager.Periods.Periods.AddRange(member.Periods.Periods);
            manager.Accounts.Accounts.AddRange(member.Accounts.Accounts);
            manager.Entries.Entries.AddRange(member.Entries.Entries);

            var reversed = await manager.SendAsync(new ReverseJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = created.Data,
                ReversalDate = March15
            });

            Assert.True(reversed.Successful);
            Assert.Equal(2, manager.Entries.Entries.Count);
            Assert.Equal(JournalEntryStatus.Reversed,
                manager.Entries.Entries.Single(entry => entry.Id == created.Data).Status);
            Assert.Equal(2, manager.LedgerLines.Lines.Count);
        }

        [Fact]
        public async Task A_period_with_drafts_cannot_be_closed_until_they_are_deleted() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);
            var periodId = harness.Periods.Periods.Single().Id;

            var created = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));

            await Assert.ThrowsAsync<DomainRuleException>(
                () => harness.SendAsync(new CloseFiscalPeriodCommand {
                    EntityId = entity.Id,
                    PeriodId = periodId
                }));

            var deleted = await harness.SendAsync(new DeleteJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = created.Data
            });
            Assert.True(deleted.Successful);

            var closed = await harness.SendAsync(new CloseFiscalPeriodCommand {
                EntityId = entity.Id,
                PeriodId = periodId
            });
            Assert.True(closed.Successful);
        }

        [Fact]
        public async Task A_posted_entry_cannot_be_deleted() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            var created = await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));
            await harness.SendAsync(new PostJournalEntryCommand {
                EntityId = entity.Id,
                EntryId = created.Data
            });

            await Assert.ThrowsAsync<DomainRuleException>(
                () => harness.SendAsync(new DeleteJournalEntryCommand {
                    EntityId = entity.Id,
                    EntryId = created.Data
                }));
        }

        [Fact]
        public async Task Entry_numbers_are_sequential_per_entity() {
            var harness = Harness();
            var (entity, cash, revenue) = Seed(harness);

            await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));
            await harness.SendAsync(SaleEntry(entity.Id, cash.Id, revenue.Id));

            Assert.Equal([1L, 2L], harness.Entries.Entries
                .Select(entry => entry.EntryNumber)
                .Order()
                .ToArray());
        }
    }
}
