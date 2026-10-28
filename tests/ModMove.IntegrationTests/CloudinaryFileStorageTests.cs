using ModMove.Infrastructure.ExternalServices.Cloudinary;

namespace ModMove.IntegrationTests;

/// <summary>Which image URLs count as ours (security rule: only our Cloudinary). No network call.</summary>
public class CloudinaryFileStorageTests
{
    private static readonly CloudinaryFileStorage Storage = new(new CloudinaryOptions
    {
        CloudName = "dvfyi3mpw",
        ApiKey = "key",
        ApiSecret = "secret",
        UploadPreset = "modmove",
    });

    [Theory]
    [InlineData("https://res.cloudinary.com/dvfyi3mpw/image/upload/v1/modmove/a.jpg", true)]
    [InlineData("https://RES.CLOUDINARY.COM/dvfyi3mpw/image/upload/a.jpg", true)]
    [InlineData("https://res.cloudinary.com/other/image/upload/a.jpg", false)]
    [InlineData("https://res.cloudinary.com/dvfyi3mpwX/image/upload/a.jpg", false)]
    [InlineData("http://res.cloudinary.com/dvfyi3mpw/image/upload/a.jpg", false)]
    [InlineData("https://evil.com/res.cloudinary.com/dvfyi3mpw/a.jpg", false)]
    [InlineData("https://res.cloudinary.com.evil.com/dvfyi3mpw/a.jpg", false)]
    [InlineData("not a url", false)]
    public void Only_our_cloud_urls_are_accepted(string url, bool ok) =>
        Assert.Equal(ok, Storage.IsOwnedUrl(url));
}
