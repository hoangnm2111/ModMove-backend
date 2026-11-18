using FluentValidation;
using ModMove.Application.Common;

namespace ModMove.Application.Features.Orders.Commands.UploadImage;

internal sealed class UploadImageCommandValidator : AbstractValidator<UploadImageCommand>
{
    public UploadImageCommandValidator() =>
        ImageUploadRules.Apply(this, x => x.Length, x => x.ContentType, x => x.Content);
}
