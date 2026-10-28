using Dapper;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Entities;

namespace ModMove.Application.Features.Users.Queries.GetUserDetail;

internal sealed class GetUserDetailQueryHandler(ISqlConnectionFactory connectionFactory)
    : IQueryHandler<GetUserDetailQuery, UserDetailResponse>
{
    private const string Sql = """
        SELECT
            u.id AS Id,
            u.full_name AS FullName,
            u.phone_number AS PhoneNumber,
            u.email AS Email,
            u.role AS Role,
            u.account_status AS AccountStatus,
            u.email_verified AS EmailVerified,
            u.avatar_url AS AvatarUrl,
            u.created_at AS CreatedAt,
            u.updated_at AS UpdatedAt,
            dp.approval_status AS DriverApprovalStatus,
            dp.online_status AS DriverOnlineStatus,
            v.license_plate AS LicensePlate,
            (SELECT COUNT(1) FROM shipping_orders so
             WHERE so.customer_id = u.id OR (dp.id IS NOT NULL AND so.driver_profile_id = dp.id)) AS TotalOrders,
            (SELECT COUNT(1) FROM shipping_orders so
             WHERE (so.customer_id = u.id OR (dp.id IS NOT NULL AND so.driver_profile_id = dp.id))
               AND so.order_status = 'Completed') AS CompletedOrders
        FROM users u
        LEFT JOIN driver_profiles dp ON dp.user_id = u.id
        LEFT JOIN vehicles v ON v.driver_profile_id = dp.id
        WHERE u.id = @Id
        """;

    public async Task<UserDetailResponse> Handle(GetUserDetailQuery request, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        var user = await connection.QueryFirstOrDefaultAsync<UserDetailResponse>(
            new CommandDefinition(Sql, new { request.Id }, cancellationToken: cancellationToken));

        return user ?? throw new NotFoundException(nameof(User), request.Id);
    }
}

