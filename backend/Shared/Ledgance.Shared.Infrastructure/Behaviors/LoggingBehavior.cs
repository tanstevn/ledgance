using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Attributes;
using Ledgance.Shared.Application.Identity;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ledgance.Shared.Infrastructure.Behaviors {
    /// <summary>
    /// Logs request names and outcomes only. Request payloads carry client and financial data
    /// and must not reach the logs.
    /// </summary>
    [PipelineOrder(0)]
    public sealed class LoggingBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull {
        private static readonly string RequestName = typeof(TRequest).Name;

        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
        private readonly ICurrentUserAccessor _currentUser;

        public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger,
            ICurrentUserAccessor currentUser) {
            _logger = logger;
            _currentUser = currentUser;
        }

        public async Task<TResponse> HandleAsync(TRequest request,
            RequestHandlerDelegate<TResponse> next, CancellationToken ct) {
            var organizationId = _currentUser.Current?.OrganizationId;
            var timestamp = Stopwatch.GetTimestamp();

            try {
                var response = await next();

                _logger.LogInformation("{Request} handled for organization {OrganizationId} in {Elapsed}",
                    RequestName, organizationId, Stopwatch.GetElapsedTime(timestamp));

                return response;
            }
            catch (Exception exception) {
                _logger.LogWarning(exception, "{Request} failed for organization {OrganizationId} in {Elapsed}",
                    RequestName, organizationId, Stopwatch.GetElapsedTime(timestamp));

                throw;
            }
        }
    }
}
