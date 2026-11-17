using FluentValidation;
using ModMove.Domain.Constants;

namespace ModMove.Application.Common;

/// <summary>Shared validation rules for JPG/PNG image uploads up to 5 MB (GB-06).</summary>
internal static class ImageUploadRules
{
    internal const long MaxBytes = BusinessRules.MaxPhotoSizeMb * 1024L * 1024L;

    public static void Apply<T>(
        AbstractValidator<T> validator,
        System.Linq.Expressions.Expression<Func<T, long>> lengthSelector,
        System.Linq.Expressions.Expression<Func<T, string>> contentTypeSelector,
        System.Linq.Expressions.Expression<Func<T, Stream>> contentSelector)
    {
        validator.RuleFor(lengthSelector)
            .InclusiveBetween(1, MaxBytes)
            .WithErrorCode("FILE_TOO_LARGE")
            .WithMessage($"The image must be between 1 byte and {BusinessRules.MaxPhotoSizeMb} MB.");

        validator.RuleFor(contentTypeSelector)
            .Must(IsSupportedContentType)
            .WithErrorCode("UNSUPPORTED_FILE_TYPE")
            .WithMessage("Only JPG or PNG images are accepted.");

        // The content type comes from the client; the first bytes tell what the file really is.
        validator.RuleFor(contentSelector)
            .Must(IsJpgOrPng)
            .WithErrorCode("UNSUPPORTED_FILE_TYPE")
            .WithMessage("The file is not a valid JPG or PNG image.");
    }

    public static bool IsSupportedContentType(string? contentType) =>
        contentType is not null
        && (contentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/jpg", StringComparison.OrdinalIgnoreCase)
            || contentType.Equals("image/png", StringComparison.OrdinalIgnoreCase));

    public static bool IsJpgOrPng(Stream? content)
    {
        if (content is null || !content.CanRead || !content.CanSeek)
            return false;

        content.Seek(0, SeekOrigin.Begin);
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

