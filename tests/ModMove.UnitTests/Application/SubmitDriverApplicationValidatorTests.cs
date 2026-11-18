using FluentValidation.TestHelper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.DriverOnboarding.Commands.SubmitDriverApplication;

namespace ModMove.UnitTests.Application;

/// <summary>UC-DRV-03: field formats and the seven required documents.</summary>
public class SubmitDriverApplicationValidatorTests
{
    private static readonly string[] Docs =
        ["https://res.cloudinary.com/demo/image/upload/a.jpg", "https://res.cloudinary.com/demo/image/upload/b.jpg", "https://res.cloudinary.com/demo/image/upload/c.jpg", "https://res.cloudinary.com/demo/image/upload/d.jpg", "https://res.cloudinary.com/demo/image/upload/e.png",
         "https://res.cloudinary.com/demo/image/upload/f.jpg", "https://res.cloudinary.com/demo/image/upload/g.jpg"];

    private static SubmitDriverApplicationCommand Valid() =>
        new(1, "51C-111.22", 900m, Docs);

    private sealed class TeamStorage : IFileStorage
    {
        public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool IsOwnedUrl(string url) => url.StartsWith("https://res.cloudinary.com/demo/", StringComparison.Ordinal);
    }

    private static TestValidationResult<SubmitDriverApplicationCommand> Check(SubmitDriverApplicationCommand c) =>
        new SubmitDriverApplicationCommandValidator(new TeamStorage()).TestValidate(c);

    [Fact]
    public void Valid_command_passes() => Check(Valid()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void Missing_back_document_url_fails_validation()
    {
        var withEmptyBack = Docs.ToArray();
        withEmptyBack[6] = "";
        Check(Valid() with { DocumentUrls = withEmptyBack })
            .ShouldHaveValidationErrorFor("DocumentUrls[6]");
    }

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
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(8)]
    public void Wrong_document_count_fails(int count) =>
        Check(Valid() with { DocumentUrls = [.. Enumerable.Range(0, count).Select(i => $"https://res.cloudinary.com/demo/image/upload/{i}.jpg")] })
            .ShouldHaveValidationErrorFor(x => x.DocumentUrls);

    [Theory]
    [InlineData("https://cdn.example.com/licence.jpg")]               // another host the Administrator would open
    [InlineData("https://res.cloudinary.com/someone-else/a.jpg")]     // Cloudinary, but not the team's cloud
    [InlineData("http://res.cloudinary.com/demo/image/upload/a.jpg")] // not https
    public void Document_outside_the_teams_Cloudinary_fails(string url) =>
        Check(Valid() with { DocumentUrls = [.. Docs[..6], url] })
            .ShouldHaveValidationErrorFor("DocumentUrls[6]");

    [Fact]
    public void Non_url_document_fails() =>
        Check(Valid() with { DocumentUrls = [.. Docs[..6], "not-a-url"] })
            .ShouldHaveValidationErrorFor("DocumentUrls[6]");
}

