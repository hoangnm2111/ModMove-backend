using Dapper;
using ModMove.Application.Common;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Users.Queries.GetMyProfile;

internal sealed class GetMyProfileQueryHandler(ISqlConnectionFactory connectionFactory, ICurrentUserService currentUser)
    : IQueryHandler<GetMyProfileQuery, ProfileResponse>
{
    private const string Sql = """
        SELECT id AS Id, full_name AS FullName, phone_number AS PhoneNumber, email AS Email,
               role AS Role, avatar_url AS AvatarUrl, email_verified AS EmailVerified
        FROM users
        WHERE id = @Id
        """;

    public async Task<ProfileResponse> Handle(GetMyProfileQuery request, CancellationToken cancellationToken)
    {
        var id = currentUser.RequireUserId();
        using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ProfileResponse>(new CommandDefinition(Sql, new { Id = id }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");
    }
}
