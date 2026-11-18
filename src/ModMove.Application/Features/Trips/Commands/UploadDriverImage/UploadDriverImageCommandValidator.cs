using FluentValidation;
using ModMove.Application.Common;

namespace ModMove.Application.Features.Trips.Commands.UploadDriverImage;

internal sealed class UploadDriverImageCommandValidator : AbstractValidator<UploadDriverImageCommand>
{
    public UploadDriverImageCommandValidator() =>
        ImageUploadRules.Apply(this, x => x.Length, x => x.ContentType, x => x.Content);
}

