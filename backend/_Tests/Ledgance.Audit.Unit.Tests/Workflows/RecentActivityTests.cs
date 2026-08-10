using Ledgance.Audit.Engagement.Application;
using Ledgance.Audit.Engagement.Application.Activity;
using Ledgance.Audit.Engagement.Application.Ports;
using Ledgance.Audit.Engagement.Domain;
using Ledgance.Audit.Unit.Tests.Support;
using Ledgance.Shared.Application.Activity;
using Ledgance.Shared.Application.Identity;
using Ledgance.Shared.Application.Models;
using Ledgance.TestInfrastructure;

namespace Ledgance.Audit.Unit.Tests.Workflows {
    internal sealed class StubActivityReader : IActivityReader {
        public List<RecordedActivity> Entries { get; } = [];

        public Task<IReadOnlyList<RecordedActivity>> ListAsync(Guid? contextId, int limit,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RecordedActivity>>(Entries
                .Where(entry => contextId is null || entry.ContextId == contextId)
                .Take(limit)
                .ToList());

        public Task<IReadOnlyList<RecordedActivity>> ListRecentAsync(string module,
            int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RecordedActivity>>(Entries
                .Where(entry => entry.Module == module)
                .OrderByDescending(entry => entry.OccurredAt)
                .Take(limit)
                .ToList());
    }

    public class RecentActivityTests {
        private static RecordedActivity Entry(Guid engagementId, string summary) =>
            new(Guid.NewGuid(), "Audit", "test.action", "Engagement", engagementId,
                summary, engagementId, Guid.NewGuid(), "actor@ledgance.test",
                DateTime.UtcNow);

        [Fact]
        public async Task The_recent_feed_shows_only_the_callers_engagements() {
            var user = TestIdentity.User(OrganizationRole.Member,
                permissions: [AuditEngagementPermissions.Read]);

            var mine = Guid.NewGuid();
            var foreign = Guid.NewGuid();

            var team = new InMemoryTeamRepository();
            team.Members.Add(EngagementTeamMember.Assign(mine, user.UserId,
                EngagementRole.Senior));

            var reader = new StubActivityReader();
            reader.Entries.Add(Entry(mine, "My engagement changed."));
            reader.Entries.Add(Entry(foreign, "Another team's engagement changed."));

            var harness = new MediatorTestHarness(user)
                .WithHandler<GetRecentAuditActivityQuery,
                    Result<IEnumerable<ActivityRow>>,
                    GetRecentAuditActivityQueryHandler>()
                .WithService<IActivityReader>(reader)
                .WithService<ITeamRepository>(team);

            var result = await harness.SendAsync(new GetRecentAuditActivityQuery());

            Assert.True(result.Successful);
            var row = Assert.Single(result.Data!);
            Assert.Equal("My engagement changed.", row.Summary);
        }
    }
}
