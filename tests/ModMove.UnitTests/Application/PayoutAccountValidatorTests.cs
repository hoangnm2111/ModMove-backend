using FluentValidation.TestHelper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Refunds;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;
using ModMove.Application.Features.Refunds.Commands.SetRefundPayoutAccount;
using ModMove.Application.Features.Refunds.Commands.SubmitComplaint;

namespace ModMove.UnitTests.Application;

/// <summary>Input rules of the payout account a customer gives for a refund (DECISIONS R-01).</summary>
public class PayoutAccountValidatorTests
{
    private sealed class TeamStorage : IFileStorage
    {
        public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public bool IsOwnedUrl(string url) => url.StartsWith("https://res.cloudinary.com/modmove/", StringComparison.Ordinal);
    }

    private static readonly IFileStorage Storage = new TeamStorage();

    public static TheoryData<string, PayoutAccountInput?, bool> PayoutAccounts => new()
    {
        { "valid", new(false, "970436", "0123456789", "NGUYEN VAN A"), true },
        { "6-digit number", new(false, "970436", "123456", "AN"), true },
        { "19-digit number", new(false, "970436", "1234567890123456789", "AN"), true },
        { "lower case holder", new(false, "970436", "0123456789", "nguyen van a"), true },
        { "suggested", new(true, null, null, null), true },
        { "5-digit number", new(false, "970436", "12345", "NGUYEN VAN A"), false },
        { "20-digit number", new(false, "970436", "12345678901234567890", "NGUYEN VAN A"), false },
        { "number with letters", new(false, "970436", "01234abc89", "NGUYEN VAN A"), false },
        { "number with Arabic-Indic digits", new(false, "970436", "٠١٢٣٤٥٦٧٨٩", "NGUYEN VAN A"), false },
        { "number with trailing newline", new(false, "970436", "0123456789\n", "NGUYEN VAN A"), false },
        { "holder with accents", new(false, "970436", "0123456789", "NGUYỄN VĂN A"), false },
        { "holder over 100 chars", new(false, "970436", "0123456789", new string('A', 101)), false },
        { "holder empty", new(false, "970436", "0123456789", ""), false },
        { "holder of one letter", new(false, "970436", "0123456789", "A"), false },
        { "holder with digits", new(false, "970436", "0123456789", "NGUYEN 2"), false },
        { "missing bank", new(false, null, "0123456789", "NGUYEN VAN A"), false },
        { "bank code over 20 chars", new(false, new string('9', 21), "0123456789", "NGUYEN VAN A"), false },
        { "suggested with a bank code", new(true, "970436", null, null), false },
        { "suggested with a number", new(true, null, "0123456789", null), false },
        { "suggested with a holder", new(true, null, null, "NGUYEN VAN A"), false },
        { "missing account", null, false },
    };

    private static void Check<T>(TestValidationResult<T> result, bool valid)
    {
        if (valid) result.ShouldNotHaveAnyValidationErrors();
        else result.ShouldHaveAnyValidationError();
    }

    [Theory]
    [MemberData(nameof(PayoutAccounts))]
    public void Complaint_payout_account_follows_R01(string _, PayoutAccountInput? account, bool valid) =>
        Check(new SubmitComplaintCommandValidator(Storage).TestValidate(
            new SubmitComplaintCommand(1, "DAMAGED_CARGO", null, null, 100_000, account!)), valid);

    [Theory]
    [MemberData(nameof(PayoutAccounts))]
    public void Incident_refund_payout_account_follows_R01(string _, PayoutAccountInput? account, bool valid) =>
        Check(new RequestIncidentRefundCommandValidator(Storage).TestValidate(
            new RequestIncidentRefundCommand(1, "Water damage", null, 100_000, account!)), valid);

    [Theory]
    [MemberData(nameof(PayoutAccounts))]
    public void Set_payout_account_follows_R01(string _, PayoutAccountInput? account, bool valid) =>
        Check(new SetRefundPayoutAccountCommandValidator().TestValidate(new SetRefundPayoutAccountCommand(1, account!)), valid);

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    public void Set_payout_account_needs_a_refund_id(long refundId, bool valid) =>
        Check(new SetRefundPayoutAccountCommandValidator().TestValidate(
            new SetRefundPayoutAccountCommand(refundId, new PayoutAccountInput(true, null, null, null))), valid);
}
