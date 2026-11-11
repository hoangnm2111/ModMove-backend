using System.Globalization;
using ModMove.Application.Common;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Features.Pricing.Commands.UpdatePlatformSettings;

internal sealed class UpdatePlatformSettingsCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository users,
    IPlatformSettingRepository settings,
    IShippingOrderRepository orders,
    IUnitOfWork unitOfWork,
    TimeProvider time) : ICommandHandler<UpdatePlatformSettingsCommand>
{
    public async Task Handle(UpdatePlatformSettingsCommand request, CancellationToken cancellationToken)
    {
        var admin = await currentUser.LoadUserAsync(users, cancellationToken);

        var requested = new Dictionary<string, decimal>();
        if (request.CommissionRate is { } rate) requested[SettingKeys.CommissionRate] = rate;
        if (request.LoadingFee is { } loadingFee) requested[SettingKeys.LoadingFee] = loadingFee;
        if (request.RequiredDeposit is { } requiredDeposit) requested[SettingKeys.RequiredDeposit] = requiredDeposit;
        if (request.MatchingRadiusKm is { } radius) requested[SettingKeys.MatchingRadiusKm] = radius;

        var rows = (await settings.GetByKeysAsync([.. requested.Keys], cancellationToken))
            .ToDictionary(s => s.SettingKey);

        // Only values that really change: re-sending today's rate must not be blocked by the lock below.
        var changes = new List<(string Key, decimal Value)>();
        foreach (var (key, value) in requested)
        {
            if (!rows.TryGetValue(key, out var row))
                throw new InvalidOperationException($"Platform setting '{key}' is missing. Run the seed."); // setup error, like IPlatformSettings

            if (decimal.Parse(row.SettingValue, NumberStyles.Number, CultureInfo.InvariantCulture) != value)
                changes.Add((key, value));
        }

        // DECISIONS C8: the commission checked when a driver accepts (GB-16) must equal the one taken at completion.
        if (changes.Any(c => c.Key == SettingKeys.CommissionRate)
            && await orders.HasOrdersInProgressAsync(cancellationToken))
        {
            throw new DomainException(
                "The commission rate cannot change while orders are in progress.", "COMMISSION_RATE_LOCKED");
        }

        if (changes.Count == 0)
            return;

        var now = time.GetUtcNow();
        foreach (var (key, value) in changes)
            rows[key].Change(value.ToString(CultureInfo.InvariantCulture), now, admin.Id); // text, always invariant

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
