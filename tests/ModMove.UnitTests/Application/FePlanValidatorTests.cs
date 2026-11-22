using FluentValidation.TestHelper;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.DriverOnboarding.Commands.RejectDriverApplication;
using ModMove.Application.Features.DriverOnboarding.Commands.ResubmitDriverApplication;
using ModMove.Application.Features.Trips.Commands.CollectCash;
using ModMove.Application.Features.Trips.Commands.UploadDriverImage;
using ModMove.Domain.Constants;

namespace ModMove.UnitTests.Application;

public class FePlanValidatorTests
{
    private sealed class StubStorage : IFileStorage
    {
        public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool IsOwnedUrl(string url) => url.StartsWith("https://res.cloudinary.com/demo/", StringComparison.Ordinal);
    }

    [Fact]
    public void RejectDriverApplicationValidator_accepts_structured_items_and_rejects_invalid_keys()
    {
        var validator = new RejectDriverApplicationCommandValidator();

        validator.TestValidate(new RejectDriverApplicationCommand(
            1,
            RejectedItems: [new(DriverApplicationItemKeys.CitizenIdFront, "Ảnh bị lóa")]))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new RejectDriverApplicationCommand(
            1,
            RejectedItems: [new("UnknownKey", "Lý do")]))
            .ShouldHaveAnyValidationError();

        // D2: drivers send no id numbers any more, so there is nothing for the Administrator to reject there.
        validator.TestValidate(new RejectDriverApplicationCommand(
            1,
            RejectedItems: [new("PersonalInfo", "Số CCCD sai")]))
            .ShouldHaveAnyValidationError();

        validator.TestValidate(new RejectDriverApplicationCommand(1, [], []))
            .ShouldHaveAnyValidationError();
    }

    [Fact]
    public void ResubmitDriverApplicationValidator_validates_only_submitted_items()
    {
        var validator = new ResubmitDriverApplicationCommandValidator(new StubStorage());

        validator.TestValidate(new ResubmitDriverApplicationCommand(
            [new(DriverApplicationItemKeys.LicenseBack, "https://res.cloudinary.com/demo/image/upload/new.jpg")]))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new ResubmitDriverApplicationCommand())
            .ShouldHaveAnyValidationError();

        validator.TestValidate(new ResubmitDriverApplicationCommand(
            [new(DriverApplicationItemKeys.LicenseBack, "https://evil.example.com/x.jpg")]))
            .ShouldHaveAnyValidationError();

        validator.TestValidate(new ResubmitDriverApplicationCommand(
            Vehicle: new ResubmitVehicleInfo(LicensePlate: "INVALID#PLATE")))
            .ShouldHaveValidationErrorFor("Vehicle.LicensePlate");
    }

    [Fact]
    public void CollectCashValidator_takes_optional_coordinates_but_checks_their_range_GB_52()
    {
        var validator = new CollectCashCommandValidator();

        validator.TestValidate(new CollectCashCommand(1, 10.77m, 106.70m)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CollectCashCommand(1, null, null)).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new CollectCashCommand(1, 91m, 106.70m)).ShouldHaveValidationErrorFor(x => x.Latitude);
        validator.TestValidate(new CollectCashCommand(1, 10.77m, 181m)).ShouldHaveValidationErrorFor(x => x.Longitude);
    }

    [Fact]
    public void UploadDriverImageValidator_emits_FILE_TOO_LARGE_and_UNSUPPORTED_FILE_TYPE_codes()
    {
        var validator = new UploadDriverImageCommandValidator();
        byte[] jpg = [0xFF, 0xD8, 0xFF, 0xE0];

        var tooLarge = validator.Validate(new UploadDriverImageCommand(new MemoryStream(jpg), "a.jpg", "image/jpeg", 5_242_881));
        Assert.False(tooLarge.IsValid);
        Assert.Equal("FILE_TOO_LARGE", new ValidationException(tooLarge.Errors).Code);

        var badType = validator.Validate(new UploadDriverImageCommand(new MemoryStream(jpg), "a.gif", "image/gif", 4));
        Assert.False(badType.IsValid);
        Assert.Equal("UNSUPPORTED_FILE_TYPE", new ValidationException(badType.Errors).Code);

        var imageJpg = validator.Validate(new UploadDriverImageCommand(new MemoryStream(jpg), "a.jpg", "image/jpg", 4));
        Assert.True(imageJpg.IsValid);
    }
}

