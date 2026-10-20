using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Exceptions;
using ModMove.Domain.Exceptions;

namespace ModMove.WebApi.Middlewares;

/// <summary>
/// Converts exceptions into RFC 7807 ProblemDetails so every client
/// (WebApp, Flutter, React Native) gets the same error contract.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var problem = ToProblemDetails(ex);
            problem.Instance = context.Request.Path;
            problem.Extensions["traceId"] = context.TraceIdentifier;

            if (problem.Status >= StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception");

            context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null,
                "application/problem+json");
        }
    }

    private static ProblemDetails ToProblemDetails(Exception ex) => ex switch
    {
        ValidationException e => new ValidationProblemDetails(e.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
        },
        NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found", e.Message),
        ForbiddenAccessException e => Problem(StatusCodes.Status403Forbidden, "Forbidden", e.Message),
        ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict", e.Message),
        DomainException e => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message),
        _ => Problem(StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred."),
    };

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };
}
