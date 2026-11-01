using FluentValidation.TestHelper;
using ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;

namespace ModMove.UnitTests.Application;

/// <summary>UC-DRV-03: field formats and the five required documents.</summary>
public class SubmitDriverApplicationValidatorTests
{
    private static readonly string[] Docs =
        ["https://cdn.x/a.jpg", "https://cdn.x/b.jpg", "https://cdn.x/c.jpg", "https://cdn.x/d.jpg", "https://cdn.x/e.png"];

    private static SubmitDriverApplicationCommand Valid() =>
        new(1, "012345678901", "B2123456789", "51C-111.22", 900m, Docs);

    private static TestValidationResult<SubmitDriverApplicationCommand> Check(SubmitDriverApplicationCommand c) =>
        new SubmitDriverApplicationCommandValidator().TestValidate(c);

    [Fact]
    public void Valid_command_passes() => Check(Valid()).ShouldNotHaveAnyValidationErrors();

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("12345678901234")]
    [InlineData("12345678A")]
    public void Bad_citizen_id_fails(string id) =>
        Check(Valid() with { CitizenIdNumber = id }).ShouldHaveValidationErrorFor(x => x.CitizenIdNumber);

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("has space 1234")]
    public void Bad_license_number_fails(string number) =>
        Check(Valid() with { DriverLicenseNumber = number }).ShouldHaveValidationErrorFor(x => x.DriverLicenseNumber);

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("51C#111.22")]
    public void Bad_plate_fails(string plate) =>
        Check(Valid() with { LicensePlate = plate }).ShouldHaveValidationErrorFor(x => x.LicensePlate);

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Non_positive_weight_fails(int kg) =>
        Check(Valid() with { MaxLoadWeightKg = kg }).ShouldHaveValidationErrorFor(x => x.MaxLoadWeightKg);

    [Fact]
    public void Vehicle_type_id_must_be_positive() =>
        Check(Valid() with { VehicleTypeId = 0 }).ShouldHaveValidationErrorFor(x => x.VehicleTypeId);

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(6)]
    public void Wrong_document_count_fails(int count) =>
        Check(Valid() with { DocumentUrls = [.. Enumerable.Range(0, count).Select(i => $"https://cdn.x/{i}.jpg")] })
            .ShouldHaveValidationErrorFor(x => x.DocumentUrls);

    [Fact]
    public void Non_url_document_fails() =>
        Check(Valid() with { DocumentUrls = [.. Docs[..4], "not-a-url"] })
            .ShouldHaveValidationErrorFor("DocumentUrls[4]");
}

