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
                workload.RequiredTier, 100));
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

    public sealed class InMemoryAiUsageMeter : IAiUsageMeter {
        private readonly Dictionary<(Guid, ProductModule, string), long> _usage = [];

        public Task<long> GetUsedAsync(Guid organizationId, ProductModule module,
            string period, CancellationToken ct) =>
            Task.FromResult(_usage.GetValueOrDefault((organizationId, module, period)));

        public Task RecordAsync(Guid organizationId, ProductModule module, string period,
            long units, CancellationToken ct) {
            var key = (organizationId, module, period);
            _usage[key] = _usage.GetValueOrDefault(key) + units;
            return Task.CompletedTask;
        }

        public void Seed(Guid organizationId, ProductModule module, long units) =>
            _usage[(organizationId, module, AiUsage.CurrentPeriod())] = units;

        public long UsedNow(Guid organizationId, ProductModule module) =>
            _usage.GetValueOrDefault((organizationId, module, AiUsage.CurrentPeriod()));
    }
}
