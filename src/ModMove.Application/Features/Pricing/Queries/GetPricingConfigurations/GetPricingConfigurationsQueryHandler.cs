using Dapper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Pricing.Queries.GetPricingConfigurations;

internal sealed class GetPricingConfigurationsQueryHandler(ISqlConnectionFactory connectionFactory, IPlatformSettings platformSettings)
    : IQueryHandler<GetPricingConfigurationsQuery, PricingConfigurationResponse>
{
    // No is_active filter: unlike the public list, the Admin also sees disabled types.
    private const string Sql = """
        SELECT id AS Id, name AS Name, max_load_weight_kg AS MaxLoadWeightKg, base_fare AS BaseFare,
               price_per_km AS PricePerKm, minimum_fare AS MinimumFare, is_active AS IsActive
        FROM vehicle_types
        ORDER BY max_load_weight_kg
        """;

    public async Task<PricingConfigurationResponse> Handle(GetPricingConfigurationsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<VehicleTypePricingResponse> vehicleTypes;
        using (var connection = connectionFactory.CreateConnection())
        {
            var rows = await connection.QueryAsync<VehicleTypePricingResponse>(
                new CommandDefinition(Sql, cancellationToken: cancellationToken));
            vehicleTypes = rows.AsList();
        }

        // IPlatformSettings is the one place that parses the stored text (invariant culture).
        var settings = new PlatformSettingsResponse(
            await platformSettings.GetDecimalAsync(SettingKeys.CommissionRate, cancellationToken),
            await platformSettings.GetDecimalAsync(SettingKeys.LoadingFee, cancellationToken),
            await platformSettings.GetDecimalAsync(SettingKeys.MinCodDeposit, cancellationToken),
            await platformSettings.GetDecimalAsync(SettingKeys.MatchingRadiusKm, cancellationToken));

        return new PricingConfigurationResponse(vehicleTypes, settings);
    }
}
