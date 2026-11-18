using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.DriverOnboarding;
using ModMove.Domain.Constants;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.Infrastructure.ExternalServices.Cloudinary;
using ModMove.Infrastructure.Persistence;
using ModMove.WebApi.Middlewares;
using ModMove.WebApi.Services;

namespace ModMove.IntegrationTests;

public class FePlanInfrastructureAndApiTests
{
    private sealed class DummyParameter : IDbDataParameter
    {
        public DbType DbType { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable => true;
        [AllowNull] public string ParameterName { get; set; } = "";
        [AllowNull] public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public object? Value { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        public int Size { get; set; }
    }

    [Fact]
    public void UtcDateTimeOffsetTypeHandler_parses_unspecified_datetime_as_UTC_and_writes_UtcDateTime()
    {
        var handler = new UtcDateTimeOffsetTypeHandler();
        var nullableHandler = new NullableUtcDateTimeOffsetTypeHandler();

        // 14:00 VN stored in MySQL DATETIME as 07:00 Unspecified
        var parsed = handler.Parse(new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Unspecified));
        Assert.Equal(new DateTimeOffset(2026, 11, 17, 7, 0, 0, TimeSpan.Zero), parsed);
        Assert.Equal(TimeSpan.Zero, parsed.Offset);

        Assert.Null(nullableHandler.Parse(DBNull.Value));
        Assert.Equal(parsed, nullableHandler.Parse(new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Unspecified)));

        var param = new DummyParameter();
        handler.SetValue(param, new DateTimeOffset(2026, 11, 17, 14, 0, 0, TimeSpan.FromHours(7)));
        Assert.Equal(new DateTime(2026, 11, 17, 7, 0, 0, DateTimeKind.Utc), param.Value);
        Assert.Equal(DbType.DateTime, param.DbType);
    }

    [Fact]
    public void UtcDateTimeOffsetJsonConverter_always_serializes_ISO_8601_with_Z_suffix()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UtcDateTimeOffsetJsonConverter());

        var vnTime = new DateTimeOffset(2026, 11, 17, 14, 0, 0, TimeSpan.FromHours(7));
        var json = JsonSerializer.Serialize(vnTime, options);

        Assert.Equal("\"2026-11-17T07:00:00Z\"", json);
    }

    [Theory]
    [MemberData(nameof(MiddlewareCases))]
    public async Task ExceptionHandlingMiddleware_maps_business_and_upload_errors_without_500(
        Exception exception, int expectedStatus, string expectedCode)
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => Task.FromException(exception),
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(expectedStatus, context.Response.StatusCode);
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var doc = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(expectedCode, doc.RootElement.GetProperty("code").GetString());
    }

    public static TheoryData<Exception, int, string> MiddlewareCases => new()
    {
        { new WrongBankAccountException("Sai số tài khoản"), 422, "WRONG_BANK_ACCOUNT" },
        { new DomainException("Upload failed", "UPLOAD_FAILED"), 422, "UPLOAD_FAILED" },
        { new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge), 413, "FILE_TOO_LARGE" },
        {
            new ValidationException([new ValidationFailure("Length", "Too big") { ErrorCode = "FILE_TOO_LARGE" }]),
            400,
            "FILE_TOO_LARGE"
        },
        {
            new ValidationException([new ValidationFailure("ContentType", "Bad type") { ErrorCode = "UNSUPPORTED_FILE_TYPE" }]),
            400,
            "UNSUPPORTED_FILE_TYPE"
        },
    };

    [Fact]
    public async Task NotConfiguredFileStorage_throws_UPLOAD_FAILED_DomainException_instead_of_500()
    {
        var storage = new NotConfiguredFileStorage();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            storage.UploadImageAsync(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]), "a.jpg", "modmove/drivers/1", CancellationToken.None));

        Assert.Equal("UPLOAD_FAILED", ex.Code);
    }

    [Fact]
    public async Task CloudinaryFileStorage_with_missing_credentials_throws_UPLOAD_FAILED_DomainException()
    {
        var storage = new CloudinaryFileStorage(new CloudinaryOptions
        {
            CloudName = "cloud",
            ApiKey = "",
            ApiSecret = "",
            UploadPreset = "modmove",
        });

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            storage.UploadImageAsync(new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0]), "a.jpg", "modmove/drivers/1", CancellationToken.None));

        Assert.Equal("UPLOAD_FAILED", ex.Code);
    }

    [Theory]
    [InlineData("\"WrongBankAccount\"", PayoutRejectionReasonCode.WrongBankAccount)]
    [InlineData("\"WRONG_BANK_ACCOUNT\"", PayoutRejectionReasonCode.WrongBankAccount)]
    [InlineData("\"wrong_bank_account\"", PayoutRejectionReasonCode.WrongBankAccount)]
    [InlineData("\"Other\"", PayoutRejectionReasonCode.Other)]
    [InlineData("\"OTHER\"", PayoutRejectionReasonCode.Other)]
    public void PayoutRejectionReasonCodeJsonConverter_accepts_PascalCase_and_SCREAMING_SNAKE_CASE(
        string json, PayoutRejectionReasonCode expected)
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new PayoutRejectionReasonCodeJsonConverter());

        var actual = JsonSerializer.Deserialize<PayoutRejectionReasonCode>(json, options);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DriverOnboardingJson_infers_structured_rejected_items_from_legacy_reasons_when_column_is_null()
    {
        var items = DriverOnboardingJson.ParseRejectedItems(
            null,
            ["LicenseFront: Ảnh mờ", "Ảnh CCCD mặt sau bị chói", "Biển số xe không khớp"]);

        Assert.NotNull(items);
        Assert.Equal(3, items!.Count);
        Assert.Equal((DriverApplicationItemKeys.LicenseFront, "Ảnh mờ"), (items[0].Key, items[0].Reason));
        Assert.Equal(DriverApplicationItemKeys.CitizenIdBack, items[1].Key);
        Assert.Equal(DriverApplicationItemKeys.VehicleInfo, items[2].Key);
    }
}
