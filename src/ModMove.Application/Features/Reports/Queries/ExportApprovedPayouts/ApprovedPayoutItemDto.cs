namespace ModMove.Application.Features.Reports.Queries.ExportApprovedPayouts;

/// <summary>
/// DTO representing a single approved payout row (driver withdrawal or customer refund) for bank export (UC-ADM-23, GB-27).
/// </summary>
public sealed record ApprovedPayoutItemDto
{
    public int Index { get; init; }
    public string PayoutReference { get; init; } = "";
    public string PayoutType { get; init; } = "";
    public string Role { get; init; } = "";
    public string BeneficiaryName { get; init; } = "";
    public string BeneficiaryAccountNumber { get; init; } = "";
    public string BeneficiaryBankName { get; init; } = "";
    public string? BeneficiaryBankBin { get; init; }
    public decimal Amount { get; init; }
    public string TransferDescription { get; init; } = "";
    public string PhoneNumber { get; init; } = "";
    public string? OrderCode { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public string? ApprovedByName { get; init; }
}
