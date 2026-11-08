using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Wallets.Commands.UpdateDriverBankAccount;

internal sealed class UpdateDriverBankAccountCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IDriverProfileRepository driverProfiles,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateDriverBankAccountCommand>
{
    public async Task Handle(UpdateDriverBankAccountCommand request, CancellationToken cancellationToken)
    {
        var (_, profile) = await DriverAccess.LoadApprovedDriverAsync(currentUser, users, driverProfiles, cancellationToken);

        profile.LinkBankAccount(
            request.BankName.Trim(),
            request.BankAccountNumber.Trim(),
            request.BankAccountHolder.Trim());

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

