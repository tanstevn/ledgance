using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Subscriptions;

namespace Ledgance.TestInfrastructure {
    public sealed class FakeAiCompletionService : IAiCompletionService {
        public List<AiWorkload> Workloads { get; } = [];

        public string Response { get; set; } = "AI response";

        public Exception? Throws { get; set; }

        public Task<AiCompletion> CompleteAsync(AiWorkload workload, CancellationToken ct) {
            Workloads.Add(workload);

            if (Throws is not null) {
                throw Throws;
            }

            return Task.FromResult(new AiCompletion(Response, "Fake", "fake-model",
                workload.RequiredTier, 100,
                new AiUsageCharge(workload.Cost, 100, false, false, null)));
        }
    }

    public sealed class FakeAiChatClient : IAiChatClient {
        private readonly Func<string, string>? _respond;

        public FakeAiChatClient(string provider, Func<string, string>? respond = null,
            Exception? throws = null) {
            Provider = provider;
            _respond = respond;
            Throws = throws;
        }

        public string Provider { get; }

        public Exception? Throws { get; set; }

        public List<(string Model, string SystemPrompt, string UserPrompt)> Calls { get; } = [];

        public Task<string> CompleteAsync(string model, string systemPrompt, string userPrompt,
            int maxOutputTokens, CancellationToken ct) {
            Calls.Add((model, systemPrompt, userPrompt));

            if (Throws is not null) {
                throw Throws;
            }

            return Task.FromResult(_respond?.Invoke(userPrompt) ?? $"{Provider} response");
        }
    }

    public sealed class FakeAgentToolClient : IAgentToolClient {
        public FakeAgentToolClient(string provider = "OpenClaw") {
            Provider = provider;
        }

        public string Provider { get; }

        public Exception? Throws { get; set; }

        public Queue<AgentTurn> Turns { get; } = new();

        public List<(string Model, string Goal, IReadOnlyList<AgentTool> Tools,
            IReadOnlyList<AgentExchange> Exchanges)> Calls { get; } = [];

        public Task<AgentTurn> NextTurnAsync(string model, string systemPrompt, string goal,
            IReadOnlyList<AgentTool> tools, IReadOnlyList<AgentExchange> exchanges,
            int maxOutputTokens, CancellationToken ct) {
            if (Throws is not null) {
                throw Throws;
            }

            Calls.Add((model, goal, tools, exchanges));

            return Task.FromResult(Turns.Count > 0
                ? Turns.Dequeue()
                : new AgentTurn($"{Provider} final answer", null));
        }
    }

    /// <summary>
    /// Mirrors the production meter's contract, including the part that matters most: reserving
    /// checks and decrements under one lock, so a test can drive concurrent callers at it and
    /// see the same refusal a real organization would.
    /// </summary>
    public sealed class InMemoryAiUsageMeter : IAiUsageMeter {
        private readonly Lock _gate = new();
        private readonly Dictionary<(Guid, ProductModule, string), long> _usage = [];
        private readonly Dictionary<Guid, (Guid Organization, ProductModule Module, string Period, long Units)> _reservations = [];

        public List<AiUsageContext> Reserved { get; } = [];

        public List<Guid> Released { get; } = [];

        public Task<AiUsageSnapshot> GetAsync(Guid organizationId, ProductModule module,
            AiUsagePeriod period, long limit, CancellationToken ct) {
            lock (_gate) {
                return Task.FromResult(new AiUsageSnapshot(period.Key, period.ResetsAt,
                    _usage.GetValueOrDefault((organizationId, module, period.Key)), limit));
            }
        }

        public Task<AiUsageReservation?> TryReserveAsync(AiUsageContext context,
            AiUsagePeriod period, long units, long limit, CancellationToken ct) {
            lock (_gate) {
                var key = (context.OrganizationId, context.Module, period.Key);
                var used = _usage.GetValueOrDefault(key);

                if (limit != EntitlementSet.Unlimited && used + units > limit) {
                    return Task.FromResult<AiUsageReservation?>(null);
                }

                _usage[key] = used + units;
                Reserved.Add(context);

                var id = Guid.NewGuid();
                _reservations[id] = (context.OrganizationId, context.Module, period.Key, units);

                return Task.FromResult<AiUsageReservation?>(
                    new AiUsageReservation(id, period.Key, units, _usage[key], limit));
            }
        }

        public Task ReleaseAsync(Guid organizationId, AiUsageReservation reservation,
            CancellationToken ct) {
            lock (_gate) {
                if (_reservations.Remove(reservation.Id, out var held)
                    && held.Organization == organizationId) {
                    var key = (held.Organization, held.Module, held.Period);
                    _usage[key] = Math.Max(0, _usage.GetValueOrDefault(key) - held.Units);
                    Released.Add(reservation.Id);
                }

                return Task.CompletedTask;
            }
        }

        public void Seed(Guid organizationId, ProductModule module, long units,
            string? period = null) =>
            _usage[(organizationId, module, period ?? AiUsage.CalendarPeriod())] = units;

        public long UsedNow(Guid organizationId, ProductModule module, string? period = null) =>
            _usage.GetValueOrDefault(
                (organizationId, module, period ?? AiUsage.CalendarPeriod()));
    }

    /// <summary>
    /// A period the test controls, so usage-window behaviour can be exercised without waiting
    /// for a calendar or a billing cycle.
    /// </summary>
    public sealed class StubAiUsagePeriodResolver : IAiUsagePeriodResolver {
        public AiUsagePeriod Period { get; set; } =
            new(AiUsage.CalendarPeriod(), null);

        public Task<AiUsagePeriod> ResolveAsync(Guid organizationId, ProductModule module,
            CancellationToken ct) =>
            Task.FromResult(Period);
    }

    public sealed class StubAiOperationCosts : IAiOperationCosts {
        public Dictionary<string, long> Overrides { get; } = [];

        public long CostOf(string capability, long declaredCost) =>
            Overrides.TryGetValue(capability, out var configured)
                ? configured
                : Math.Max(1, declaredCost);
    }
}
