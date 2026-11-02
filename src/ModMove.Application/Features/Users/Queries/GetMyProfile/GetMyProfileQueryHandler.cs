using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;
using ModMove.Domain.Enums;

namespace ModMove.Application.Features.Users.Queries.GetMyProfile;

internal sealed class GetMyProfileQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyProfileQuery, ProfileResponse>
{
    private const string Sql = """
        SELECT id AS Id, full_name AS FullName, phone_number AS PhoneNumber, email AS Email,
               role AS Role, email_verified AS EmailVerified, account_status AS AccountStatus
        FROM users
        WHERE id = @Id
        """;

    public async Task<ProfileResponse> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var id = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(Sql, new { Id = id }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        // A token outlives a deletion or an Admin lock by up to 15 minutes; refuse like the other Account endpoints.
        if (row.AccountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        return new ProfileResponse(row.Id, row.FullName, row.PhoneNumber, row.Email, row.Role, row.EmailVerified);
    }

    private sealed record Row(
        long Id, string FullName, string PhoneNumber, string Email, string Role, bool EmailVerified, string AccountStatus);
}
