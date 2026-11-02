using Dapper;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Monitoring.Queries.GetDispatcherOrdersSummary;

internal sealed class GetDispatcherOrdersSummaryQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetDispatcherOrdersSummaryQuery, DispatcherOrdersSummaryResponse>
{
    private const string Sql = """
        SELECT order_status AS OrderStatus, COUNT(*) AS Count
        FROM shipping_orders
        GROUP BY order_status;
        """;

    public async Task<DispatcherOrdersSummaryResponse> Handle(
        GetDispatcherOrdersSummaryQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(Sql, cancellationToken: cancellationToken));

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
            counts[row.OrderStatus] = row.Count;

        int CountOf(string status) => counts.TryGetValue(status, out var n) ? n : 0;

        var pending = CountOf("Pending");
        var accepted = CountOf("Accepted");
        var arrived = CountOf("Arrived");
        var pickedUp = CountOf("PickedUp");
        var inTransit = CountOf("InTransit");
        var delivered = CountOf("Delivered");
        var completed = CountOf("Completed");
        var cancelled = CountOf("Cancelled");
        var total = pending + accepted + arrived + pickedUp + inTransit + delivered + completed + cancelled;

        return new DispatcherOrdersSummaryResponse(
            total, pending, accepted, arrived, pickedUp, inTransit, delivered, completed, cancelled);
    }

    private sealed class Row
    {
        public string OrderStatus { get; init; } = "";
        public int Count { get; init; }
    }
}

