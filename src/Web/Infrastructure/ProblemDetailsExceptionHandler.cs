using backend.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace backend.Web.Infrastructure;

/// <summary>
/// Global exception → HTTP response mapper.
///
/// Every exception that escapes the MediatR pipeline lands here.
/// Known exceptions are mapped to structured <see cref="ProblemDetails"/> responses;
/// unknown exceptions produce a generic 500 so the stack trace is never leaked.
///
/// Log levels:
///   400 / 401 / 403 / 404  → Warning  (expected, client-side mistakes)
///   500                     → Error    (unexpected, needs investigation)
/// </summary>
public class ProblemDetailsExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ProblemDetailsExceptionHandler> _logger;

    public ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception   exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, problemDetails) = exception switch
        {
            ValidationException ve => (
                StatusCodes.Status400BadRequest,
                (ProblemDetails)new ValidationProblemDetails(ve.Errors)
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title  = "Validation failed.",
                    Type   = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
                }),

            NotFoundException ne => (
                StatusCodes.Status404NotFound,
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title  = "Resource not found.",
                    Detail = ne.Message,
                    Type   = "https://tools.ietf.org/html/rfc9110#section-15.5.5"
                }),

            UnauthorizedAccessException ue => (
                StatusCodes.Status401Unauthorized,
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title  = "Unauthorized.",
                    Detail = ue.Message,
                    Type   = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                }),

            ForbiddenAccessException => (
                StatusCodes.Status403Forbidden,
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title  = "Forbidden.",
                    Type   = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
                }),

            _ => (
                StatusCodes.Status500InternalServerError,
                new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title  = "An unexpected error occurred.",
                    Type   = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
                })
        };

        // ── Logging ───────────────────────────────────────────────────────────
        var method = httpContext.Request.Method;
        var path   = httpContext.Request.Path;

        if (statusCode >= 500)
            _logger.LogError(exception,
                "Unhandled exception on {Method} {Path} → {StatusCode}",
                method, path, statusCode);
        else
            _logger.LogWarning(exception,
                "{ExceptionType} on {Method} {Path} → {StatusCode}: {Message}",
                exception.GetType().Name, method, path, statusCode, exception.Message);

        // ── Response ──────────────────────────────────────────────────────────
        httpContext.Response.StatusCode = statusCode;

        // Must pass the runtime type so ValidationProblemDetails.Errors
        // is included in the serialized output (not dropped by base-type inference).
        await httpContext.Response.WriteAsJsonAsync(
            problemDetails,
            problemDetails.GetType(),
            cancellationToken: cancellationToken);

        return true;
    }
}
