using ModMove.Application.Common.Messaging;

namespace ModMove.Application.Features.Reports.Queries.ExportApprovedPayouts;

/// <summary>
/// Exports all approved payout requests (driver withdrawals and customer refunds)
/// as a standard bank-ready Office Open XML (.xlsx) file (UC-ADM-20, UC-ADM-23, UC-ADM-24, GB-27).
/// </summary>
public sealed record ExportApprovedPayoutsQuery(
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Role = null) : IQuery<ApprovedPayoutsFileResponse>;

public sealed record ApprovedPayoutsFileResponse(
    byte[] Content,
    string ContentType,
    string FileName);
