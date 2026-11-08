using System.Data;
using Dapper;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.Application.Common;

/// <summary>
/// Shared write-side and read-side access checks for signed-in drivers (DECISIONS B8, GB-03).
/// Ensures a locked or deleted account is immediately refused even with a valid JWT.
/// </summary>
internal static class DriverAccess
{
    private const string ContextSql = """
        SELECT u.account_status AS AccountStatus,
               dp.id AS DriverProfileId,
               dp.approval_status AS ApprovalStatus,
               dp.bank_name AS BankName,
               dp.bank_account_number AS BankAccountNumber,
               dp.bank_account_holder AS BankAccountHolder
        FROM users u
        LEFT JOIN driver_profiles dp ON dp.user_id = u.id
        WHERE u.id = @UserId
        """;

    public static async Task<(User Driver, DriverProfile Profile)> LoadApprovedDriverAsync(
        ICurrentUserService currentUser,
        IUserRepository users,
        IDriverProfileRepository driverProfiles,
        CancellationToken cancellationToken)
    {
        var user = await currentUser.LoadUserAsync(users, cancellationToken);
        var profile = await driverProfiles.GetByUserIdAsync(user.Id, cancellationToken);
        if (profile is null || profile.ApprovalStatus != ApprovalStatus.Approved)
            throw new DomainException("Driver documents must be approved first.", "DRIVER_NOT_APPROVED");

        return (user, profile);
    }

    public static async Task<DriverReadContext> LoadReadContextAsync(
        IDbConnection connection, long userId, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(ContextSql, new { UserId = userId }, cancellationToken: cancellationToken))
            ?? throw new UnauthorizedException("Sign in again.");

        if (row.AccountStatus != nameof(AccountStatus.Active))
            throw CurrentUserExtensions.InactiveAccount();

        return new DriverReadContext(
            row.DriverProfileId,
            row.ApprovalStatus,
            row.BankName,
            row.BankAccountNumber,
            row.BankAccountHolder);
    }

    public static async Task<DriverReadContext> LoadApprovedReadContextAsync(
        IDbConnection connection, long userId, CancellationToken cancellationToken)
    {
        var context = await LoadReadContextAsync(connection, userId, cancellationToken);
        if (context.DriverProfileId is null || context.ApprovalStatus != nameof(ApprovalStatus.Approved))
            throw new DomainException("Driver documents must be approved first.", "DRIVER_NOT_APPROVED");

        return context;
    }

    internal sealed record DriverReadContext(
        long? DriverProfileId,
        string? ApprovalStatus,
        string? BankName,
        string? BankAccountNumber,
        string? BankAccountHolder);

    private sealed class Row
    {
        public string AccountStatus { get; init; } = "";
        public long? DriverProfileId { get; init; }
        public string? ApprovalStatus { get; init; }
        public string? BankName { get; init; }
        public string? BankAccountNumber { get; init; }
        public string? BankAccountHolder { get; init; }
    }
}

