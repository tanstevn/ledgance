using Ledgance.Shared.Application.Ai;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Subscriptions;
using Ledgance.Shared.Infrastructure.Ai;
using Ledgance.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ledgance.Shared.Unit.Tests.Ai {
    public class AgentRunnerServiceTests {
        private readonly FakeCurrentUserAccessor _user = new(TestIdentity.User());
        private readonly FakeEntitlementService _entitlements = new();
        private readonly InMemoryAiUsageMeter _usage = new();
        private readonly FakeAgentToolClient _openClaw = new(AiProviders.OpenClaw);
        private readonly FakeAiChatClient _anthropic;
        private readonly List<string> _toolInvocations = [];

        private string _anthropicReply =
            """{"action":"final","answer":"fallback answer"}""";

        public AgentRunnerServiceTests() {
            _anthropic = new FakeAiChatClient(AiProviders.Anthropic, _ => _anthropicReply);
        }

        private AgentRunnerService Runner() =>
            new(_user, _entitlements, _usage,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_openClaw], [_anthropic], NullLogger<AgentRunnerService>.Instance);

        private AgentWorkload Workload(int maxSteps = 8,
            Func<JsonElement, CancellationToken, Task<string>>? execute = null) =>
            new(ProductModule.Audit, "audit.agent", "Investigate the engagement.",
                "You are a test agent.", [
                    new AgentTool("get_data", "Reads test data.",
                        """{"type":"object","properties":{}}""",
                        (arguments, ct) => {
                            _toolInvocations.Add("get_data");
                            return execute is null
                                ? Task.FromResult("data-result")
                                : execute(arguments, ct);
                        })
                ], maxSteps);

        private static AgentTurn ToolCall(string tool, string arguments = "{}") =>
            new(null, new AgentToolCall(tool, arguments));

        [Fact]
        public async Task A_plan_below_the_agentic_tier_is_refused_before_any_provider_call() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditOrganization);

            var exception = await Assert.ThrowsAsync<EntitlementException>(
                () => Runner().RunAsync(Workload(), default));

            Assert.Contains(AiTiers.Agentic, exception.Message);
            Assert.Empty(_openClaw.Calls);
            Assert.Empty(_toolInvocations);
        }

        [Fact]
        public async Task A_run_executes_tools_then_returns_the_final_answer() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);
            _openClaw.Turns.Enqueue(ToolCall("get_data"));
            _openClaw.Turns.Enqueue(new AgentTurn("The data says X.", null));

            var run = await Runner().RunAsync(Workload(), default);

            Assert.Equal("The data says X.", run.Answer);
            Assert.Equal(AiProviders.OpenClaw, run.Provider);
            Assert.Equal(2, run.TurnsUsed);

            var step = Assert.Single(run.Steps);
            Assert.Equal("get_data", step.Tool);
            Assert.Equal("data-result", step.Result);

            Assert.Equal(["get_data"], _toolInvocations);
            Assert.Equal("data-result",
                _openClaw.Calls[1].Exchanges.Single().Result);
            Assert.Equal(2, _usage.UsedNow(TestIdentity.DefaultOrganizationId,
                ProductModule.Audit));
        }

        [Fact]
        public async Task An_unknown_tool_is_reported_back_instead_of_failing_the_run() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);
            _openClaw.Turns.Enqueue(ToolCall("drop_database"));
            _openClaw.Turns.Enqueue(new AgentTurn("Recovered.", null));

            var run = await Runner().RunAsync(Workload(), default);

            Assert.Equal("Recovered.", run.Answer);
            var step = Assert.Single(run.Steps);
            Assert.Contains("not an available tool", step.Result);
            Assert.Empty(_toolInvocations);
        }

        [Fact]
        public async Task An_authorization_denial_inside_a_tool_is_contained_not_bypassed() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);
            _openClaw.Turns.Enqueue(ToolCall("get_data"));
            _openClaw.Turns.Enqueue(new AgentTurn("Done without it.", null));

            var run = await Runner().RunAsync(Workload(execute: (_, _) =>
                throw new ForbiddenException("Denied.")), default);

            Assert.Equal("Done without it.", run.Answer);
            var step = Assert.Single(run.Steps);
            Assert.StartsWith("Access denied", step.Result);
        }

        [Fact]
        public async Task The_step_limit_forces_a_final_turn_with_no_tools() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);

            for (var i = 0; i < 5; i++) {
                _openClaw.Turns.Enqueue(ToolCall("get_data"));
            }

            var run = await Runner().RunAsync(Workload(maxSteps: 2), default);

            Assert.Equal(2, run.Steps.Count);
            Assert.Equal(3, run.TurnsUsed);

            var finalCall = _openClaw.Calls[^1];
            Assert.Empty(finalCall.Tools);
            Assert.Contains("final answer now", finalCall.Goal);
        }

        [Fact]
        public async Task An_openclaw_failure_falls_back_to_the_chat_provider_chain() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);
            _openClaw.Throws = new HttpRequestException("OpenClaw is down.");

            var run = await Runner().RunAsync(Workload(), default);

            Assert.Equal("fallback answer", run.Answer);
            Assert.Equal(AiProviders.Anthropic, run.Provider);
            Assert.Single(_anthropic.Calls);
        }

        [Fact]
        public async Task The_chat_fallback_can_drive_tool_calls_through_the_json_protocol() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);
            _openClaw.Throws = new HttpRequestException("OpenClaw is down.");

            var replied = false;
            var adapter = new FakeAiChatClient(AiProviders.Anthropic, prompt => {
                if (replied) {
                    Assert.Contains("data-result", prompt);
                    return """{"action":"final","answer":"used the tool"}""";
                }

                replied = true;
                return """{"action":"call_tool","tool":"get_data","arguments":{}}""";
            });

            var runner = new AgentRunnerService(_user, _entitlements, _usage,
                new ConfiguredAiModelRouter(Options.Create(new AiSettings())),
                [_openClaw], [adapter], NullLogger<AgentRunnerService>.Instance);

            var run = await runner.RunAsync(Workload(), default);

            Assert.Equal("used the tool", run.Answer);
            Assert.Equal(["get_data"], _toolInvocations);
            Assert.Single(run.Steps);
        }

        [Fact]
        public async Task The_monthly_unit_limit_stops_a_run_before_the_next_turn() {
            _entitlements.With(ProductModule.Audit, PlanCode.AuditFirm);
            _usage.Seed(TestIdentity.DefaultOrganizationId, ProductModule.Audit, 60000);

            await Assert.ThrowsAsync<EntitlementException>(
                () => Runner().RunAsync(Workload(), default));

            Assert.Empty(_openClaw.Calls);
        }
    }
}
