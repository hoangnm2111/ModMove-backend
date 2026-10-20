using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application.Common.Exceptions;
using ModMove.Domain.Exceptions;
using ModMove.WebApi.Middlewares;

namespace ModMove.IntegrationTests;

/// <summary>The error contract clients rely on: HTTP status + a stable `code` field.</summary>
public class ErrorContractTests
{
    public static TheoryData<Exception, int, string> Cases => new()
    {
        { new UnauthorizedException(), 401, "UNAUTHORIZED" },
        { new ForbiddenAccessException(), 403, "FORBIDDEN" },
        { new NotFoundException("Order", 1), 404, "NOT_FOUND" },
        { new ConflictException(), 409, "CONFLICT" },
        { new DomainException("rule"), 422, "BUSINESS_RULE" },
        { new DomainException("rule", "MSG67"), 422, "MSG67" },
        { new InvalidOperationException("boom"), 500, "SERVER_ERROR" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Exception_becomes_problem_details_with_status_and_code(Exception exception, int status, string code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new ExceptionHandlingMiddleware(_ => throw exception, NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Validation_error_lists_the_failing_fields()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var failure = new FluentValidation.Results.ValidationFailure("Email", "'Email' must not be empty.");
        var middleware = new ExceptionHandlingMiddleware(_ => throw new ValidationException([failure]), NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal("VALIDATION", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("'Email' must not be empty.", body.RootElement.GetProperty("errors").GetProperty("Email")[0].GetString());
    }
}
