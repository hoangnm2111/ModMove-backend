using FluentValidation;

namespace ModMove.Application.Features.MasterData.Commands.CreateMasterDataItem;

public sealed class CreateMasterDataItemCommandValidator : AbstractValidator<CreateMasterDataItemCommand>
{
    public CreateMasterDataItemCommandValidator()
    {
        RuleFor(x => x.Category).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(65535);
    }
}
