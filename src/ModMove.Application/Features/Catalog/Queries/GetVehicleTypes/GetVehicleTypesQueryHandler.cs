using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Catalog.Queries.GetVehicleTypes;

/// <summary>
/// Reference query: read side = Dapper over ISqlConnectionFactory, no repository, no change tracking.
/// Columns are snake_case in MySQL, so alias each one to the PascalCase name of the response member.
/// </summary>
internal sealed class GetVehicleTypesQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetVehicleTypesQuery, IReadOnlyList<VehicleTypeResponse>>
{
    private const string Sql = """
        SELECT id AS Id, name AS Name, max_load_weight_kg AS MaxLoadWeightKg,
               cargo_bed_length_cm AS CargoBedLengthCm, cargo_bed_width_cm AS CargoBedWidthCm,
               cargo_bed_height_cm AS CargoBedHeightCm, base_fare AS BaseFare,
               price_per_km AS PricePerKm, minimum_fare AS MinimumFare
        FROM vehicle_types
        WHERE is_active = 1
        ORDER BY max_load_weight_kg
        """;

    public async Task<IReadOnlyList<VehicleTypeResponse>> Handle(GetVehicleTypesQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<VehicleTypeResponse>(new CommandDefinition(Sql, cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
