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
            // Runtime type, so ValidationProblemDetails keeps its per-field `errors`.
            await context.Response.WriteAsJsonAsync(problem, problem.GetType(), (System.Text.Json.JsonSerializerOptions?)null,
                "application/problem+json");
        }
    }

    private static ProblemDetails ToProblemDetails(Exception ex) => ex switch
    {
        ValidationException e => WithCode(
            new ValidationProblemDetails(e.Errors) { Status = StatusCodes.Status400BadRequest, Title = "Validation failed" },
            e.Code ?? "VALIDATION"),
        BadHttpRequestException e when e.StatusCode == StatusCodes.Status413PayloadTooLarge => Problem(
            StatusCodes.Status413PayloadTooLarge, "File too large", e.Message, "FILE_TOO_LARGE"),
        BadHttpRequestException e => Problem(
            e.StatusCode, "Bad request", e.Message, "VALIDATION"),
        UnauthorizedException e => Problem(StatusCodes.Status401Unauthorized, "Unauthorized", e.Message, "UNAUTHORIZED"),
        NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found", e.Message, "NOT_FOUND"),
        ForbiddenAccessException e => Problem(StatusCodes.Status403Forbidden, "Forbidden", e.Message, e.Code ?? "FORBIDDEN"),
        ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict", e.Message, "CONFLICT"),
        WrongBankAccountException e => Problem(
            StatusCodes.Status422UnprocessableEntity, "Wrong bank account", e.Message, WrongBankAccountException.ErrorCode),
        DomainException e => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message, e.Code ?? "BUSINESS_RULE"),
        _ => Problem(StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred.", "SERVER_ERROR"),
    };

    // Clients branch on `code` (stable), never on the English `detail`.
    private static ProblemDetails Problem(int status, string title, string detail, string code) =>
        WithCode(new ProblemDetails { Status = status, Title = title, Detail = detail }, code);

    private static ProblemDetails WithCode(ProblemDetails problem, string code)
    {
        problem.Extensions["code"] = code;
        return problem;
    }
}
