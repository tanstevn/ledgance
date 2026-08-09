using FluentValidation;
using Ledgance.Shared.Application.Abstractions;
using Ledgance.Shared.Application.Attributes;

namespace Ledgance.Shared.Infrastructure.Behaviors {
    [PipelineOrder(300)]
    public sealed class ValidationBehavior<TRequest, TResponse>
        : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull {
        private readonly IEnumerable<IValidator<TRequest>> _validators;

        public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) {
            _validators = validators;
        }

        public async Task<TResponse> HandleAsync(TRequest request,
            RequestHandlerDelegate<TResponse> next, CancellationToken ct) {
            ArgumentNullException.ThrowIfNull(request);

            var context = new ValidationContext<TRequest>(request);

            var failures = (await Task.WhenAll(_validators
                    .Select(validator => validator.ValidateAsync(context, ct))))
                .SelectMany(result => result.Errors)
                .Where(failure => failure is not null)
                .ToList();

            if (failures.Count > 0) {
                throw new ValidationException(failures);
            }

            return await next();
        }
    }
}
