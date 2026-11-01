using FluentValidation;
using ModMove.Domain.Constants;

namespace ModMove.Application.Features.Orders.Commands.UploadImage;

internal sealed class UploadImageCommandValidator : AbstractValidator<UploadImageCommand>
{
    private const long MaxBytes = BusinessRules.MaxPhotoSizeMb * 1024L * 1024L;

    public UploadImageCommandValidator()
    {
        RuleFor(x => x.Length).InclusiveBetween(1, MaxBytes).WithMessage("The image must be at most 5 MB.");
        RuleFor(x => x.ContentType).Must(t => t is "image/jpeg" or "image/png").WithMessage("Only JPG or PNG images are accepted.");
        // The content type comes from the client; the first bytes tell what the file really is.
        RuleFor(x => x.Content).Must(IsJpgOrPng).WithMessage("The file is not a JPG or PNG image.");
    }

    private static bool IsJpgOrPng(Stream content)
    {
        if (!content.CanRead || !content.CanSeek)
            return false;

        Span<byte> head = stackalloc byte[4];
        var read = content.Read(head);
        content.Seek(0, SeekOrigin.Begin);
        if (read < 4)
            return false;

        var jpg = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
        var png = head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
        return jpg || png;
    }
}
