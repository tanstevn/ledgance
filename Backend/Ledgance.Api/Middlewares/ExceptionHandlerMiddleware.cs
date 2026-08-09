using FluentValidation;
using Ledgance.Shared.Application.Models;
using System.Net;

namespace Ledgance.Api.Middlewares {
    public class ExceptionHandlerMiddleware {
        private readonly RequestDelegate _next;

        public ExceptionHandlerMiddleware(RequestDelegate next) {
            _next = next;
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
        }

        private static async Task WriteErrorResponse(HttpContext context,
            HttpStatusCode statusCode, object errorObject) {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)statusCode;

            await context.Response.WriteAsJsonAsync(errorObject);
        }
    }
}
