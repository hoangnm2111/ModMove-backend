using FluentValidation;

namespace ModMove.Application.Features.MasterData.Commands.UpdateMasterDataItem;

public sealed class UpdateMasterDataItemCommandValidator : AbstractValidator<UpdateMasterDataItemCommand>
{
    public UpdateMasterDataItemCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(65535);
    }
}
