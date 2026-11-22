using ModMove.Infrastructure.ExternalServices.Cloudinary;

namespace ModMove.IntegrationTests;

/// <summary>NFR 2.3: private driver documents on Cloudinary.</summary>
public class CloudinaryUrlsTests
{
    [Fact]
    public void The_signature_is_dropped_so_the_saved_url_cannot_open_the_photo()
    {
        Assert.Equal(
            "https://res.cloudinary.com/demo/image/authenticated/v1731/modmove/drivers/2/documents/abc.jpg",
            CloudinaryUrls.WithoutSignature("https://res.cloudinary.com/demo/image/authenticated/s--Ab12_x--/v1731/modmove/drivers/2/documents/abc.jpg"));
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/authenticated/v1731/modmove/drivers/2/documents/abc.jpg")]
    [InlineData("https://res.cloudinary.com/demo/image/authenticated/s--Ab12_x--/v1731/modmove/drivers/2/documents/abc.jpg")]
    public void An_authenticated_url_of_our_cloud_gives_its_public_id_and_format(string url)
    {
        Assert.True(CloudinaryUrls.TryParseAuthenticated(url, "demo", out var publicId, out var format));
        Assert.Equal(("modmove/drivers/2/documents/abc", "jpg"), (publicId, format));
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v1/modmove/drivers/2/pickup.jpg")]       // public photo
    [InlineData("https://res.cloudinary.com/other/image/authenticated/v1/modmove/drivers/2/a.jpg")]   // another cloud
    [InlineData("https://cdn.example.com/a.jpg")]
    [InlineData("not a url")]
    public void Anything_else_is_left_alone(string url)
    {
        Assert.False(CloudinaryUrls.TryParseAuthenticated(url, "demo", out _, out _));
    }

    [Fact]
    public void A_view_link_for_a_private_document_is_a_signed_download_that_expires()
    {
        var storage = new CloudinaryFileStorage(new CloudinaryOptions { CloudName = "demo", ApiKey = "123", ApiSecret = "secret" });

        var link = storage.CreateViewUrl("https://res.cloudinary.com/demo/image/authenticated/v1/modmove/drivers/2/documents/abc.jpg");

        Assert.StartsWith("https://api.cloudinary.com/v1_1/demo/image/download?", link);
        Assert.Contains("expires_at=", link);
        Assert.Contains("signature=", link);
        Assert.Contains("type=authenticated", link);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/a.jpg", storage.CreateViewUrl("https://res.cloudinary.com/demo/image/upload/a.jpg"));
    }
}
