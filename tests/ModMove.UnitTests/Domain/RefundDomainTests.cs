using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

/// <summary>
/// Unit tests for Refund domain entity &amp; DriverWallet/WalletTransaction withdrawal edge cases (Happy cases &amp; Edge cases).
/// </summary>
public class RefundDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    // ==========================================
    // 1. Refund Creation & Dispatcher Proposal (DECISIONS B9, 3.3)
    // ==========================================

    [Fact]
    public void Cancellation_refund_sets_proposed_amount_immediately_and_can_be_approved_without_dispatcher_B9()
    {
        var refund = Refund.Request(
            orderId: 10, incidentId: null, RefundSourceType.Cancellation,
            reason: "Thay đổi kế hoạch", evidenceUrls: null, requestedAmount: 180_000m, reasonCode: "CHANGED_PLAN");

        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
        Assert.Equal(180_000m, refund.RequestedAmount);
        Assert.Equal(180_000m, refund.ProposedAmount);
        Assert.Equal("CHANGED_PLAN", refund.ReasonCode);

        var versionBefore = refund.RowVersion;
        refund.Approve(adminId: 99, Now, "Duyệt hoàn tiền hủy đơn");

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Equal(99L, refund.ApprovedBy);
        Assert.Equal(Now, refund.DecidedAt);
        Assert.Equal("Duyệt hoàn tiền hủy đơn", refund.DecisionNote);
        Assert.Equal(versionBefore + 1, refund.RowVersion);
    }

    [Theory]
    [InlineData(RefundSourceType.Complaint)]
    [InlineData(RefundSourceType.Incident)]
    public void Complaint_and_incident_refunds_start_without_proposed_amount_and_require_dispatcher_proposal(RefundSourceType source)
    {
        var refund = Refund.Request(
            orderId: 10, incidentId: source == RefundSourceType.Incident ? 5 : null, source,
            reason: "Hàng bị trầy", evidenceUrls: ["a.jpg"], requestedAmount: 100_000m, reasonCode: "DAMAGED_CARGO");

        Assert.Null(refund.ProposedAmount);

        // Edge case: Admin cannot approve before Dispatcher proposes
        Assert.Throws<DomainException>(() => refund.Approve(adminId: 99, Now));

        // Happy case: Dispatcher proposes partial amount (including 0)
        refund.Propose(amount: 60_000m, reason: "Trầy nhẹ ở góc bàn", dispatcherId: 7);
        Assert.Equal(60_000m, refund.ProposedAmount);
        Assert.Equal("Trầy nhẹ ở góc bàn", refund.ProposalReason);
        Assert.Equal(7L, refund.ProposedBy);

        refund.Approve(adminId: 99, Now, "Đồng ý mức đề xuất 60k");
        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100_001)]
    public void Dispatcher_proposal_outside_0_to_requested_amount_throws_DomainException(decimal invalidAmount)
    {
        var refund = Refund.Request(10, null, RefundSourceType.Complaint, "Hỏng hàng", ["a.jpg"], 100_000m);

        Assert.Throws<DomainException>(() => refund.Propose(invalidAmount, "Lý do", dispatcherId: 7));
    }

    [Fact]
    public void Cancellation_refund_cannot_be_proposed_or_asked_for_more_info_by_dispatcher()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Cancellation, "Hủy", null, 180_000m);

        var proposeEx = Assert.Throws<DomainException>(() => refund.Propose(100_000m, "Giảm", 7));
        var infoEx = Assert.Throws<DomainException>(() => refund.RequestMoreInfo("Bổ sung ảnh"));

        Assert.Equal("REFUND_NOT_FOR_DISPATCHER", proposeEx.Code);
        Assert.Equal("REFUND_NOT_FOR_DISPATCHER", infoEx.Code);
    }

    // ==========================================
    // 2. RejectRefund & Manual Payout (MarkProcessed) Edge Cases
    // ==========================================

    [Fact]
    public void Admin_can_reject_requested_refund_and_cannot_approve_or_reject_twice()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Complaint, "Hỏng", ["a.jpg"], 100_000m);

        refund.RejectRefund(adminId: 99, Now, "Bằng chứng không hợp lệ");

        Assert.Equal(RefundStatus.Rejected, refund.RefundStatus);
        Assert.Equal(99L, refund.ApprovedBy);
        Assert.Equal("Bằng chứng không hợp lệ", refund.DecisionNote);

        // Edge case: cannot approve, reject, or payout a Rejected refund
        Assert.Throws<DomainException>(() => refund.Approve(99, Now));
        Assert.Throws<DomainException>(() => refund.RejectRefund(99, Now));
        Assert.Throws<DomainException>(() => refund.MarkProcessed("FT123", Now));
    }

    [Fact]
    public void Manual_payout_only_allowed_when_Approved_and_cannot_be_processed_twice_GB27()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Cancellation, "Hủy đơn", null, 180_000m);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);

        // Edge case: cannot payout while still Requested
        Assert.Throws<DomainException>(() => refund.MarkProcessed("FT000", Now));

        refund.Approve(adminId: 99, Now);
        var payoutTime = Now.AddMinutes(15);
        refund.MarkProcessed("FT2630512345", payoutTime);

        Assert.Equal(RefundStatus.Processed, refund.RefundStatus);
        Assert.Equal("FT2630512345", refund.BankReferenceCode);
        Assert.Equal(payoutTime, refund.ProcessedAt);

        // Edge case: cannot payout twice
        Assert.Throws<DomainException>(() => refund.MarkProcessed("FT999", payoutTime.AddMinutes(1)));
    }

    // ==========================================
    // 2b. Payout account (R-01)
    // ==========================================

    [Fact]
    public void Approve_with_a_zero_proposal_is_REFUND_ZERO_PROPOSAL()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Complaint, "Hàng vỡ", null, 100_000m);
        refund.Propose(0m, "Không đủ căn cứ", 5);

        var ex = Assert.Throws<DomainException>(() => refund.Approve(99, Now));

        Assert.Equal("REFUND_ZERO_PROPOSAL", ex.Code);
        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);

        refund.RejectRefund(99, Now); // the only way to close it
        Assert.Equal(RefundStatus.Rejected, refund.RefundStatus);
    }

    private static Refund ApprovedCancellationRefund()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Cancellation, "Hủy đơn", null, 180_000m);
        refund.Approve(adminId: 99, Now);
        return refund;
    }

    [Theory]
    [InlineData(RefundStatus.Requested)]
    [InlineData(RefundStatus.Approved)]
    [InlineData(RefundStatus.Failed)]
    public void SetPayoutAccount_is_allowed_while_Requested_Approved_or_Failed_R01(RefundStatus status)
    {
        var refund = Refund.Request(10, null, RefundSourceType.Cancellation, "Hủy đơn", null, 180_000m);
        if (status != RefundStatus.Requested) refund.Approve(99, Now);
        if (status == RefundStatus.Failed)
        {
            refund.SetPayoutAccount("970436", "Vietcombank", "1111111111", "NGUYEN VAN A", Now); // the account the transfer failed on
            refund.MarkFailed("Sai số tài khoản");
        }
        var versionBefore = refund.RowVersion;

        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);

        Assert.Equal("970436", refund.PayoutBankCode);
        Assert.Equal("Vietcombank", refund.PayoutBankName);
        Assert.Equal("0123456789", refund.PayoutAccountNumber);
        Assert.Equal("NGUYEN VAN A", refund.PayoutAccountHolder);
        Assert.Equal(Now, refund.PayoutInfoUpdatedAt);
        Assert.Equal(versionBefore + 1, refund.RowVersion);
    }

    [Theory]
    [InlineData(RefundStatus.Processed)]
    [InlineData(RefundStatus.Rejected)]
    [InlineData(RefundStatus.Withdrawn)]
    public void SetPayoutAccount_on_a_closed_refund_is_REFUND_PAYOUT_LOCKED(RefundStatus status)
    {
        var refund = Refund.Request(10, null, RefundSourceType.Complaint, "Hỏng", null, 100_000m);
        switch (status)
        {
            case RefundStatus.Withdrawn:
                refund.Withdraw();
                break;
            case RefundStatus.Rejected:
                refund.RejectRefund(99, Now);
                break;
            default:
                refund.Propose(100_000m, "Đủ", 7);
                refund.Approve(99, Now);
                refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
                refund.MarkProcessed("FT1", Now);
                break;
        }

        var ex = Assert.Throws<DomainException>(() =>
            refund.SetPayoutAccount("970415", "VietinBank", "9876543210", "TRAN VAN B", Now));
        Assert.Equal("REFUND_PAYOUT_LOCKED", ex.Code);
    }

    [Fact]
    public void SetPayoutAccount_on_a_failed_refund_returns_it_to_Approved_R01()
    {
        var refund = ApprovedCancellationRefund();
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        refund.MarkFailed("Sai số tài khoản");

        refund.SetPayoutAccount("970436", "Vietcombank", "0123456780", "NGUYEN VAN A", Now.AddHours(1));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Null(refund.PayoutFailureReason);
    }

    [Fact]
    public void SetPayoutAccount_with_the_same_account_on_a_failed_refund_is_REFUND_PAYOUT_UNCHANGED_R01()
    {
        var refund = ApprovedCancellationRefund();
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        refund.MarkFailed("Sai số tài khoản");
        var versionBefore = refund.RowVersion;

        var ex = Assert.Throws<DomainException>(() =>
            refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now.AddHours(1)));

        Assert.Equal("REFUND_PAYOUT_UNCHANGED", ex.Code);
        Assert.Equal(RefundStatus.Failed, refund.RefundStatus);
        Assert.Equal("Sai số tài khoản", refund.PayoutFailureReason);
        Assert.Equal(versionBefore, refund.RowVersion);
    }

    [Fact]
    public void SetPayoutAccount_with_the_same_account_is_fine_while_the_refund_is_not_Failed_R01()
    {
        var refund = ApprovedCancellationRefund();
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);

        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now.AddHours(1));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
    }

    [Fact]
    public void MarkProcessed_without_a_payout_account_is_REFUND_BANK_ACCOUNT_REQUIRED()
    {
        var refund = ApprovedCancellationRefund();

        var ex = Assert.Throws<DomainException>(() => refund.MarkProcessed("FT1", Now));

        Assert.Equal("REFUND_BANK_ACCOUNT_REQUIRED", ex.Code);
        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
    }

    [Fact]
    public void MarkFailed_without_a_payout_account_is_REFUND_BANK_ACCOUNT_REQUIRED_R01()
    {
        var refund = ApprovedCancellationRefund();

        var ex = Assert.Throws<DomainException>(() => refund.MarkFailed("Lỗi chuyển khoản"));

        Assert.Equal("REFUND_BANK_ACCOUNT_REQUIRED", ex.Code);
        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
    }

    [Fact]
    public void MarkFailed_keeps_the_reason()
    {
        var refund = ApprovedCancellationRefund();
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);

        refund.MarkFailed("Tài khoản không tồn tại");

        Assert.Equal(RefundStatus.Failed, refund.RefundStatus);
        Assert.Equal("Tài khoản không tồn tại", refund.PayoutFailureReason);
    }

    [Fact]
    public void HasPayoutAccount_is_false_until_set()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Cancellation, "Hủy đơn", null, 180_000m);
        Assert.False(refund.HasPayoutAccount);

        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);

        Assert.True(refund.HasPayoutAccount);
    }

    // ==========================================
    // 3. Info Request & Customer Withdraw Edge Cases
    // ==========================================

    [Fact]
    public void Customer_reply_to_info_request_enforces_max_5_photos_7_4()
    {
        var refund = Refund.Request(10, null, RefundSourceType.Complaint, "Hỏng", ["1.jpg", "2.jpg"], 100_000m);

        // Edge case: cannot reply when Dispatcher has not asked for info
        var noReq = Assert.Throws<DomainException>(() => refund.ReplyToInfoRequest("Trả lời", ["3.jpg"]));
        Assert.Equal("NO_INFO_REQUEST", noReq.Code);

        refund.RequestMoreInfo("Chụp rõ vết nứt");

        // Edge case: 2 existing + 4 new = 6 photos (> 5 limit)
        var tooMany = Assert.Throws<DomainException>(() =>
            refund.ReplyToInfoRequest("Trả lời", ["3.jpg", "4.jpg", "5.jpg", "6.jpg"]));
        Assert.Equal("TOO_MANY_PHOTOS", tooMany.Code);

        // Happy case: 2 existing + 3 new = 5 photos
        refund.ReplyToInfoRequest("Đã chụp thêm 3 góc", ["3.jpg", "4.jpg", "5.jpg"]);
        Assert.Equal("Đã chụp thêm 3 góc", refund.CustomerReply);
        Assert.Equal(5, refund.EvidenceUrls!.Count);
    }

    [Fact]
    public void Customer_can_withdraw_complaint_while_requested_but_never_cancellation_refund()
    {
        var cancellation = Refund.Request(10, null, RefundSourceType.Cancellation, "Hủy", null, 180_000m);
        Assert.Throws<DomainException>(() => cancellation.Withdraw());

        var complaint = Refund.Request(10, null, RefundSourceType.Complaint, "Hỏng", null, 50_000m);
        complaint.Withdraw();
        Assert.Equal(RefundStatus.Withdrawn, complaint.RefundStatus);

        // Cannot withdraw again or approve after withdrawn
        Assert.Throws<DomainException>(() => complaint.Withdraw());
        Assert.Throws<DomainException>(() => complaint.Approve(99, Now));
    }

    // ==========================================
    // 4. DriverWallet & WalletTransaction Withdrawal Edge Cases
    // ==========================================

    [Fact]
    public void WalletTransaction_propagates_WalletId_and_blocks_non_withdrawal_completion_or_rejection()
    {
        var wallet = DriverWallet.Create(driverProfileId: 7);
        typeof(DriverWallet).BaseType!.BaseType!.BaseType!
            .GetProperty("Id")!.SetValue(wallet, 42L);

        var tripTx = wallet.CreditTripEarning(orderId: 100, fare: 200_000m, commission: 20_000m);
        var withdrawalTx = wallet.RequestWithdrawal(100_000m);

        Assert.Equal(42L, tripTx.WalletId);
        Assert.Equal(42L, withdrawalTx.WalletId);

        // Edge case: TripEarning transaction cannot be completed or rejected as a withdrawal
        Assert.Throws<DomainException>(() => wallet.CompleteWithdrawal(tripTx, adminId: 1, "FT123", Now));
        Assert.Throws<DomainException>(() => wallet.RejectWithdrawal(tripTx, adminId: 1, Now));

        // Happy case: Complete pending withdrawal
        wallet.CompleteWithdrawal(withdrawalTx, adminId: 1, "FT888", Now);
        Assert.Equal(WalletTransactionStatus.Completed, withdrawalTx.TransactionStatus);
        Assert.Equal("FT888", withdrawalTx.ReferenceCode);
        Assert.Equal(1L, withdrawalTx.ProcessedBy);
        Assert.Equal(Now, withdrawalTx.ProcessedAt);

        // Edge case: Cannot reject an already completed withdrawal
        Assert.Throws<DomainException>(() => wallet.RejectWithdrawal(withdrawalTx, adminId: 1, Now));
    }
}

