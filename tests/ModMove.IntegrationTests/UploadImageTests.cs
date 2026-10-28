using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Orders.Commands.UploadImage;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>POST api/customer/uploads: JPG/PNG up to 5 MB (GB-06), checked by content, through the real pipeline.</summary>
public class UploadImageTests
{
    private static readonly byte[] Jpg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A];

    private static User Customer() => User.Create("An", "0900000001", "an@x.vn", "h:secret123", UserRole.Customer, emailVerified: true);

    private static UploadImageCommand Upload(byte[] bytes, string contentType, long? length = null) =>
        new(new MemoryStream(bytes), "photo", contentType, length ?? bytes.Length);

    [Fact]
    public async Task Jpg_is_uploaded_to_the_customer_folder()
    {
        var app = TestApp.SignedInAs(Customer());

        var result = await app.Sender.Send(Upload(Jpg, "image/jpeg"));

        Assert.StartsWith("https://res.cloudinary.com/demo/", result.Url);
        Assert.Equal("modmove/customers/1", app.Files.Uploaded.Single().Folder);
    }

    [Fact]
    public async Task Png_is_accepted_GB06()
    {
        var app = TestApp.SignedInAs(Customer());

        await app.Sender.Send(Upload(Png, "image/png"));

        Assert.Single(app.Files.Uploaded);
    }

    [Fact]
    public async Task File_over_5_mb_is_rejected_GB06()
    {
        var app = TestApp.SignedInAs(Customer());

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(Upload(Jpg, "image/jpeg", length: 5_242_881)));
        Assert.Empty(app.Files.Uploaded);
    }

    [Fact]
    public async Task Gif_is_rejected_GB06()
    {
        var app = TestApp.SignedInAs(Customer());

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(Upload([0x47, 0x49, 0x46, 0x38], "image/gif")));
    }

    [Fact]
    public async Task Renamed_exe_is_rejected()
    {
        var app = TestApp.SignedInAs(Customer());

        await Assert.ThrowsAsync<ValidationException>(() => app.Sender.Send(Upload([0x4D, 0x5A, 0x90, 0x00], "image/jpeg")));
        Assert.Empty(app.Files.Uploaded);
    }

    [Fact]
    public async Task Locked_account_cannot_upload()
    {
        var user = Customer();
        user.ChangeStatus(AccountStatus.Locked);
        var app = TestApp.SignedInAs(user);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => app.Sender.Send(Upload(Jpg, "image/jpeg")));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }
}
