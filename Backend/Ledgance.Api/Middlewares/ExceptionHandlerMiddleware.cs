using FluentValidation;
using Ledgance.Shared.Application.Exceptions;
using Ledgance.Shared.Application.Models;
using System.Net;

namespace Ledgance.Api.Middlewares {
    public class ExceptionHandlerMiddleware {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionHandlerMiddleware> _logger;

        public ExceptionHandlerMiddleware(RequestDelegate next,
            ILogger<ExceptionHandlerMiddleware> logger) {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context) {
            try {
                await _next(context);
            }
            catch (ArgumentNullException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.BadRequest, errorObject);
            }
            catch (ValidationException ex) {
                var errorObject = Result<object>
                    .MultipleErrors(ex.Errors
                        .Select(err => err.ErrorMessage));

                await WriteErrorResponse(context,
                    HttpStatusCode.BadRequest, errorObject);
            }
            catch (UnauthenticatedException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.Unauthorized, errorObject);
            }
            catch (ForbiddenException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.Forbidden, errorObject);
            }
            catch (DomainRuleException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.Conflict, errorObject);
            }
            catch (EntitlementException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.PaymentRequired, errorObject);
            }
            catch (InvalidOperationException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.InternalServerError, errorObject);
            }
            catch (OperationCanceledException ex) {
                var errorObject = Result<object>
                    .Error(ex.Message);

                await WriteErrorResponse(context,
                    HttpStatusCode.Gone, errorObject);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Unhandled exception while processing {Path}.",
                    context.Request.Path);

                // Unexpected failures must not leak exception detail to the caller.
                var errorObject = Result<object>
                    .Error("An unexpected error occurred.");

                await WriteErrorResponse(context,
                    HttpStatusCode.InternalServerError, errorObject);
            }
        }

        private static async Task WriteErrorResponse(HttpContext context,
            HttpStatusCode statusCode, object errorObject) {
            if (context.Response.HasStarted) {
                return;
            }

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsJsonAsync(errorObject);
        }
    }
}
