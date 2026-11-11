using FluentValidation.TestHelper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Payments.Commands.CreateOrderPayment;
using ModMove.Application.Features.Payments.Commands.ProcessPayOsWebhook;
using ModMove.Application.Features.Refunds.Commands.ApproveRefund;
using ModMove.Application.Features.Refunds.Commands.MarkRefundPayoutFailed;
using ModMove.Application.Features.Refunds.Commands.ProcessRefundPayout;
using ModMove.Application.Features.Refunds.Commands.RejectRefund;
using ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;
using ModMove.Application.Features.Wallets.Commands.ApproveWithdrawal;
using ModMove.Application.Features.Wallets.Commands.RejectWithdrawal;
using ModMove.Application.Features.Wallets.Queries.GetAdminWithdrawals;
using ModMove.Domain.Enums;

namespace ModMove.UnitTests.Application;

/// <summary>
/// Unit tests for Payment (PayOS), Admin Refund, and Admin Withdrawal Validators (Happy cases &amp; Edge cases).
/// </summary>
public class PaymentRefundWithdrawalValidatorTests
{
    // ==========================================
    // 1. CreateOrderPaymentCommandValidator (Task 1.2)
    // ==========================================

    [Theory]
    [InlineData(1L, PaymentPlatform.Web)]
    [InlineData(999L, PaymentPlatform.App)]
    public void CreateOrderPayment_validator_happy_path(long orderId, PaymentPlatform platform)
    {
        var validator = new CreateOrderPaymentCommandValidator();
        var command = new CreateOrderPaymentCommand(orderId, platform);

        validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-10L)]
    public void CreateOrderPayment_validator_non_positive_order_id_fails(long invalidOrderId)
    {
        var validator = new CreateOrderPaymentCommandValidator();
        var command = new CreateOrderPaymentCommand(invalidOrderId, PaymentPlatform.Web);

        validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.OrderId);
    }

    [Fact]
    public void CreateOrderPayment_validator_invalid_platform_enum_fails()
    {
        var validator = new CreateOrderPaymentCommandValidator();
        var command = new CreateOrderPaymentCommand(1L, (PaymentPlatform)99);

        validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Platform);
    }

    // ==========================================
    // 2. ProcessPayOsWebhookCommandValidator (Task 1.3)
    // ==========================================

    [Fact]
    public void ProcessPayOsWebhook_validator_happy_path()
    {
        var validator = new ProcessPayOsWebhookCommandValidator();
        var command = new ProcessPayOsWebhookCommand("""{"code":"00","data":{"orderCode":123}}""");

        validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcessPayOsWebhook_validator_empty_raw_body_fails(string emptyBody)
    {
        var validator = new ProcessPayOsWebhookCommandValidator();
        var command = new ProcessPayOsWebhookCommand(emptyBody);

        validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.RawBody);
    }

    // ==========================================
    // 3. GetAdminRefundsQueryValidator (Task 2.1)
    // ==========================================

    [Fact]
    public void GetAdminRefunds_validator_happy_path()
    {
        var validator = new GetAdminRefundsQueryValidator();

        validator.TestValidate(new GetAdminRefundsQuery()).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new GetAdminRefundsQuery(1, 100, RefundStatus.Approved, RefundSourceType.Complaint, "MM2026"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, 20, nameof(GetAdminRefundsQuery.Page))]
    [InlineData(-1, 20, nameof(GetAdminRefundsQuery.Page))]
    [InlineData(10_001, 20, nameof(GetAdminRefundsQuery.Page))]
    [InlineData(1, 0, nameof(GetAdminRefundsQuery.PageSize))]
    [InlineData(1, 101, nameof(GetAdminRefundsQuery.PageSize))]
    public void GetAdminRefunds_validator_invalid_paging_fails(int page, int pageSize, string expectedField)
    {
        var validator = new GetAdminRefundsQueryValidator();
        var query = new GetAdminRefundsQuery(page, pageSize);

        validator.TestValidate(query).ShouldHaveValidationErrorFor(expectedField);
    }

    [Fact]
    public void GetAdminRefunds_validator_invalid_enum_filters_fail()
    {
        var validator = new GetAdminRefundsQueryValidator();

        validator.TestValidate(new GetAdminRefundsQuery(Status: (RefundStatus)99))
            .ShouldHaveValidationErrorFor(x => x.Status);

        validator.TestValidate(new GetAdminRefundsQuery(SourceType: (RefundSourceType)99))
            .ShouldHaveValidationErrorFor(x => x.SourceType);
    }

    // ==========================================
    // 4. ApproveRefundCommandValidator & RejectRefundCommandValidator (Task 2.2)
    // ==========================================

    [Fact]
    public void ApproveRefund_and_RejectRefund_validators_happy_path()
    {
        var approveValidator = new ApproveRefundCommandValidator();
        var rejectValidator = new RejectRefundCommandValidator();
        var maxNote = new string('A', 1000);

        approveValidator.TestValidate(new ApproveRefundCommand(1, null)).ShouldNotHaveAnyValidationErrors();
        approveValidator.TestValidate(new ApproveRefundCommand(1, maxNote)).ShouldNotHaveAnyValidationErrors();

        rejectValidator.TestValidate(new RejectRefundCommand(1, null)).ShouldNotHaveAnyValidationErrors();
        rejectValidator.TestValidate(new RejectRefundCommand(1, maxNote)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-5L)]
    public void ApproveRefund_and_RejectRefund_validators_invalid_id_or_too_long_note_fail(long invalidId)
    {
        var approveValidator = new ApproveRefundCommandValidator();
        var rejectValidator = new RejectRefundCommandValidator();
        var tooLongNote = new string('A', 1001);

        approveValidator.TestValidate(new ApproveRefundCommand(invalidId, "OK"))
            .ShouldHaveValidationErrorFor(x => x.RefundId);
        approveValidator.TestValidate(new ApproveRefundCommand(1, tooLongNote))
            .ShouldHaveValidationErrorFor(x => x.Note);

        rejectValidator.TestValidate(new RejectRefundCommand(invalidId, "Lý do"))
            .ShouldHaveValidationErrorFor(x => x.RefundId);
        rejectValidator.TestValidate(new RejectRefundCommand(1, tooLongNote))
            .ShouldHaveValidationErrorFor(x => x.Note);
    }

    // ==========================================
    // 5. ProcessRefundPayoutCommandValidator (Task 2.3)
    // ==========================================

    [Fact]
    public void ProcessRefundPayout_validator_happy_path()
    {
        var validator = new ProcessRefundPayoutCommandValidator();

        validator.TestValidate(new ProcessRefundPayoutCommand(1, "FT2630512345")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new ProcessRefundPayoutCommand(1, new string('X', 50))).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, "FT123", nameof(ProcessRefundPayoutCommand.RefundId))]
    [InlineData(-1, "FT123", nameof(ProcessRefundPayoutCommand.RefundId))]
    [InlineData(1, "", nameof(ProcessRefundPayoutCommand.BankReferenceCode))]
    [InlineData(1, "   ", nameof(ProcessRefundPayoutCommand.BankReferenceCode))]
    public void ProcessRefundPayout_validator_edge_cases_fail(long refundId, string refCode, string expectedField)
    {
        var validator = new ProcessRefundPayoutCommandValidator();

        validator.TestValidate(new ProcessRefundPayoutCommand(refundId, refCode))
            .ShouldHaveValidationErrorFor(expectedField);
    }

    [Fact]
    public void ProcessRefundPayout_validator_reference_code_exceeding_50_chars_fails()
    {
        var validator = new ProcessRefundPayoutCommandValidator();

        validator.TestValidate(new ProcessRefundPayoutCommand(1, new string('X', 51)))
            .ShouldHaveValidationErrorFor(x => x.BankReferenceCode);
    }

    [Fact]
    public void MarkRefundPayoutFailed_validator_happy_path()
    {
        var validator = new MarkRefundPayoutFailedCommandValidator();

        validator.TestValidate(new MarkRefundPayoutFailedCommand(1, "Sai số tài khoản")).ShouldNotHaveAnyValidationErrors();
        validator.TestValidate(new MarkRefundPayoutFailedCommand(1, new string('X', 1000))).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, "Sai số", nameof(MarkRefundPayoutFailedCommand.RefundId))]
    [InlineData(-1, "Sai số", nameof(MarkRefundPayoutFailedCommand.RefundId))]
    [InlineData(1, "", nameof(MarkRefundPayoutFailedCommand.Reason))]
    [InlineData(1, "   ", nameof(MarkRefundPayoutFailedCommand.Reason))]
    public void MarkRefundPayoutFailed_validator_edge_cases_fail(long refundId, string reason, string expectedField)
    {
        var validator = new MarkRefundPayoutFailedCommandValidator();

        validator.TestValidate(new MarkRefundPayoutFailedCommand(refundId, reason))
            .ShouldHaveValidationErrorFor(expectedField);
    }

    [Fact]
    public void MarkRefundPayoutFailed_validator_reason_exceeding_1000_chars_fails()
    {
        var validator = new MarkRefundPayoutFailedCommandValidator();

        validator.TestValidate(new MarkRefundPayoutFailedCommand(1, new string('X', 1001)))
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }

    // ==========================================
    // 6. GetAdminWithdrawalsQueryValidator (Task 3.1)
    // ==========================================

    [Theory]
    [InlineData(null)]
    [InlineData("Pending")]
    [InlineData("pending")]
    [InlineData("Completed")]
    [InlineData("Rejected")]
    public void GetAdminWithdrawals_validator_happy_path(string? status)
    {
        var validator = new GetAdminWithdrawalsQueryValidator();
        var query = new GetAdminWithdrawalsQuery(Page: 1, PageSize: 20, Status: status, Search: "0912");

        validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, 20, "Pending", nameof(GetAdminWithdrawalsQuery.Page))]
    [InlineData(-2, 20, "Pending", nameof(GetAdminWithdrawalsQuery.Page))]
    [InlineData(1, 0, "Pending", nameof(GetAdminWithdrawalsQuery.PageSize))]
    [InlineData(1, 101, "Pending", nameof(GetAdminWithdrawalsQuery.PageSize))]
    [InlineData(1, 20, "UnknownStatus", nameof(GetAdminWithdrawalsQuery.Status))]
    public void GetAdminWithdrawals_validator_edge_cases_fail(int page, int pageSize, string? status, string expectedField)
    {
        var validator = new GetAdminWithdrawalsQueryValidator();
        var query = new GetAdminWithdrawalsQuery(page, pageSize, status);

        validator.TestValidate(query).ShouldHaveValidationErrorFor(expectedField);
    }

    // ==========================================
    // 7. ApproveWithdrawalCommandValidator & RejectWithdrawalCommandValidator (Tasks 3.2 & 3.3)
    // ==========================================

    [Fact]
    public void ApproveWithdrawal_and_RejectWithdrawal_validators_happy_path()
    {
        var approveValidator = new ApproveWithdrawalCommandValidator();
        var rejectValidator = new RejectWithdrawalCommandValidator();

        approveValidator.TestValidate(new ApproveWithdrawalCommand(10, "FT26305999")).ShouldNotHaveAnyValidationErrors();
        approveValidator.TestValidate(new ApproveWithdrawalCommand(10, new string('9', 50))).ShouldNotHaveAnyValidationErrors();

        rejectValidator.TestValidate(new RejectWithdrawalCommand(10, null)).ShouldNotHaveAnyValidationErrors();
        rejectValidator.TestValidate(new RejectWithdrawalCommand(10, new string('R', 1000))).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(0, "FT123", nameof(ApproveWithdrawalCommand.TransactionId))]
    [InlineData(-5, "FT123", nameof(ApproveWithdrawalCommand.TransactionId))]
    [InlineData(1, "", nameof(ApproveWithdrawalCommand.BankReferenceCode))]
    [InlineData(1, "   ", nameof(ApproveWithdrawalCommand.BankReferenceCode))]
    public void ApproveWithdrawal_validator_edge_cases_fail(long txId, string refCode, string expectedField)
    {
        var validator = new ApproveWithdrawalCommandValidator();

        validator.TestValidate(new ApproveWithdrawalCommand(txId, refCode))
            .ShouldHaveValidationErrorFor(expectedField);
    }

    [Fact]
    public void ApproveWithdrawal_and_RejectWithdrawal_validators_max_length_edge_cases_fail()
    {
        var approveValidator = new ApproveWithdrawalCommandValidator();
        var rejectValidator = new RejectWithdrawalCommandValidator();

        approveValidator.TestValidate(new ApproveWithdrawalCommand(1, new string('X', 51)))
            .ShouldHaveValidationErrorFor(x => x.BankReferenceCode);

        rejectValidator.TestValidate(new RejectWithdrawalCommand(0, "Lý do"))
            .ShouldHaveValidationErrorFor(x => x.TransactionId);

        rejectValidator.TestValidate(new RejectWithdrawalCommand(1, new string('R', 1001)))
            .ShouldHaveValidationErrorFor(x => x.Reason);
    }
}

