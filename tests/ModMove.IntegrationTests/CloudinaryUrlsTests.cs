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

    private static readonly CloudinaryFileStorage Demo =
        new(new CloudinaryOptions { CloudName = "demo", ApiKey = "123", ApiSecret = "secret" });

    private const string Versioned = "https://res.cloudinary.com/demo/image/authenticated/v1731/modmove/customers/3/abc.jpg";
    private const string Stored = "https://res.cloudinary.com/demo/image/authenticated/modmove/customers/3/abc.jpg";

    [Fact]
    public void A_signed_view_link_round_trips_to_the_stored_url()
    {
        var link = Demo.CreateViewUrl(Versioned);

        Assert.True(Demo.IsOwnedUrl(link));
        Assert.Equal(Stored, Demo.ToStoredUrl(link));
        Assert.True(CloudinaryUrls.TryParseAuthenticated(Demo.ToStoredUrl(link), "demo", out _, out _));
    }

    [Fact]
    public void An_expired_signed_link_is_still_stored() // DEF-011: a guest draft keeps the link for 24 h
    {
        var expired = System.Text.RegularExpressions.Regex.Replace(Demo.CreateViewUrl(Versioned), @"expires_at=\d+", "expires_at=1");

        Assert.Contains("expires_at=1&", expired + "&");
        Assert.True(Demo.IsOwnedUrl(expired));
        Assert.Equal(Stored, Demo.ToStoredUrl(expired));
    }

    [Theory]
    [InlineData("https://api.cloudinary.com/v1_1/other/image/download?public_id=a&format=jpg&type=authenticated")] // another cloud
    [InlineData("https://api.cloudinary.com/v1_1/demo/image/download?public_id=a&format=jpg&type=upload")]         // another type
    [InlineData("https://api.cloudinary.com/v1_1/demo/image/download?public_id=a&format=jpg&type=upload&type=authenticated")]
    [InlineData("https://api.cloudinary.com/v1_1/demo/image/download?format=jpg&type=authenticated")]              // no photo
    [InlineData("https://api.cloudinary.com/v1_1/demo/image/download?public_id=a&format=..%2Fx&type=authenticated")]
    [InlineData("https://api.cloudinary.com/v1_1/demo/image/destroy?public_id=a&format=jpg&type=authenticated")]   // another API call
    [InlineData("http://api.cloudinary.com/v1_1/demo/image/download?public_id=a&format=jpg&type=authenticated")]
    public void Download_links_of_another_cloud_or_type_are_not_owned(string url)
    {
        Assert.False(Demo.IsOwnedUrl(url));
        Assert.Equal(url, Demo.ToStoredUrl(url));
    }

    [Theory]
    [InlineData("https://res.cloudinary.com/demo/image/upload/v1/modmove/customers/3/abc.jpg")]
    [InlineData("https://res.cloudinary.com/other/image/authenticated/s--Ab12_x--/v1/a.jpg")]
    [InlineData("not a url")]
    public void Public_urls_are_stored_unchanged(string url) => Assert.Equal(url, Demo.ToStoredUrl(url));

    [Fact]
    public void A_signed_delivery_url_is_stored_without_its_signature() =>
        Assert.Equal(Versioned, Demo.ToStoredUrl(Versioned.Replace("/authenticated/", "/authenticated/s--Ab12_x--/")));

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
