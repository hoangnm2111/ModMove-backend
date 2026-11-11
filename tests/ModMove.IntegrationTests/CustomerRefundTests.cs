using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Refunds;
using ModMove.Application.Features.Refunds.Commands.ReplyToInfoRequest;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefund;
using ModMove.Application.Features.Refunds.Commands.SubmitComplaint;
using ModMove.Application.Features.Refunds.Commands.WithdrawRefund;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Customer side of complaints and refunds (UC-CUS-17, 18, 21, 22, 24; DECISIONS 7.3, 7.4, B9, C10).</summary>
public class CustomerRefundTests
{
    private const string Photo = "https://res.cloudinary.com/demo/image/upload/modmove/customers/1/a.jpg";

    private static SubmitComplaintCommand Complaint(ShippingOrder order, decimal amount = 100_000, IReadOnlyList<string>? photos = null) =>
        new(order.Id, "DAMAGED_CARGO", " Hàng vỡ ", photos ?? [Photo], amount, TestPayout.Valid);

    private static InTransitIncident AddIncident(TripSetup s, ShippingOrder order)
    {
        var incident = InTransitIncident.Report(order.Id, s.Driver.Id, IncidentType.VehicleBreakdown, "Xe hỏng", null, null, null);
        s.App.Incidents.Add(incident);
        return incident;
    }

    // ---- Submit complaint (UC-CUS-21, 22) ----

    [Fact]
    public async Task Complaint_within_24_hours_of_delivery_waits_for_a_dispatcher_7_3()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        var result = await s.App.Sender.Send(Complaint(order));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(refund.Id, result.RefundId);
        Assert.Equal((RefundSourceType.Complaint, RefundStatus.Requested, "Hàng vỡ"), (refund.SourceType, refund.RefundStatus, refund.Reason));
        Assert.Equal("DAMAGED_CARGO", refund.ReasonCode);
        Assert.Equal(100_000m, refund.RequestedAmount);
        Assert.Equal([Photo], refund.EvidenceUrls!);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        var sent = Assert.Single(s.App.Realtime.Sent);
        Assert.Equal("dispatchers", sent.Target);
        Assert.Equal("COMPLAINT_SUBMITTED", Assert.IsType<Notification>(sent.Payload).Type);
    }

    // ---- Payout account (R-01) ----

    [Fact]
    public async Task Complaint_keeps_the_payout_account_with_an_upper_case_holder_and_the_bank_name_from_the_list_R01()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var account = new PayoutAccountInput(false, "970436", "0123456789", "  nguyen   van a ");

        await s.App.Sender.Send(Complaint(order) with { PayoutAccount = account });

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.True(refund.HasPayoutAccount);
        Assert.Equal(("970436", "Vietcombank", "0123456789", "NGUYEN VAN A"),
            (refund.PayoutBankCode, refund.PayoutBankName, refund.PayoutAccountNumber, refund.PayoutAccountHolder));
        Assert.Equal(s.App.Time.Now, refund.PayoutInfoUpdatedAt);
    }

    [Fact]
    public async Task Incident_refund_keeps_the_payout_account_R01()
    {
        var s = new TripSetup();
        var incident = AddIncident(s, s.PickedUp());

        await s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, TestPayout.Valid));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(("970436", "Vietcombank", "0123456789", "NGUYEN VAN A"),
            (refund.PayoutBankCode, refund.PayoutBankName, refund.PayoutAccountNumber, refund.PayoutAccountHolder));
    }

    [Fact]
    public async Task Complaint_and_incident_refund_without_a_payout_account_are_a_400_R01()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var incident = AddIncident(s, order);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(Complaint(order) with { PayoutAccount = null! }));
        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, null!)));

        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task A_bank_that_is_not_in_the_BANK_list_is_a_400_R01()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var unknown = TestPayout.Valid with { BankCode = "999999" };

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(Complaint(order) with { PayoutAccount = unknown }));

        Assert.Empty(s.App.Refunds.Items);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_bank_that_was_switched_off_is_a_400_R01()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        s.App.MasterData.Items.Single(i => i.Category == "BANK" && i.Code == "970436").SetActive(false);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(Complaint(order)));

        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Reusing_the_PayOS_account_without_one_on_record_is_NO_SUGGESTED_PAYOUT_ACCOUNT_R01()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(Complaint(order) with { PayoutAccount = new PayoutAccountInput(true, null, null, null) }));

        Assert.Equal("NO_SUGGESTED_PAYOUT_ACCOUNT", error.Code);
        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Complaint_can_reuse_the_PayOS_payer_account_on_record_R01()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var tx = PaymentTransaction.ForOrderViaPayOs(order.Id, order.TotalFee, 2001L, "https://pay.test/1", TripSetup.Now);
        tx.MarkPaid(TripSetup.Now.AddMinutes(1));
        tx.RecordPayer("970436", "0011001234567", "Trần Thị Bình");
        s.App.Payments.Add(tx);

        await s.App.Sender.Send(Complaint(order) with { PayoutAccount = new PayoutAccountInput(true, null, null, null) });

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(("970436", "Vietcombank", "0011001234567", "TRAN THI BINH"),
            (refund.PayoutBankCode, refund.PayoutBankName, refund.PayoutAccountNumber, refund.PayoutAccountHolder));
    }

    [Fact]
    public async Task One_open_complaint_per_order_B9()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        await s.App.Sender.Send(Complaint(order));

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(Complaint(order)));

        Assert.Single(s.App.Refunds.Items);
    }

    [Fact]
    public async Task A_new_complaint_is_allowed_after_the_first_was_withdrawn()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        await s.App.Sender.Send(Complaint(order));
        s.App.Refunds.Items[0].Withdraw();

        await s.App.Sender.Send(Complaint(order));

        Assert.Equal(2, s.App.Refunds.Items.Count);
    }

    [Fact]
    public async Task Complaint_after_24_hours_is_refused_7_3()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        s.App.Time.Now = TripSetup.Now.AddHours(24);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Complaint(order)));

        Assert.Equal("COMPLAINT_WINDOW_CLOSED", error.Code);
        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Amount_above_the_order_total_is_refused_B9()
    {
        var s = new TripSetup();
        var order = s.Delivered(); // total 180,000

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Complaint(order, amount: 180_001)));

        Assert.Equal("REFUND_AMOUNT_INVALID", error.Code);
    }

    [Fact]
    public async Task Another_customers_order_is_refused_C10()
    {
        var s = new TripSetup();
        var order = s.Delivered(customerId: s.Other.Id);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Complaint(order)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task At_most_five_evidence_photos_from_our_storage_7_4()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(Complaint(order, photos: Enumerable.Repeat(Photo, 6).ToList())));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(Complaint(order, photos: ["https://evil.example/a.jpg"])));
    }

    [Theory]
    [InlineData("", 100_000)]
    [InlineData("DAMAGED_CARGO", 0)]
    public async Task Reason_code_and_a_positive_amount_are_required(string code, int amount)
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new SubmitComplaintCommand(order.Id, code, null, null, amount, TestPayout.Valid)));
    }

    [Fact]
    public async Task Code_alone_stores_the_name_from_the_list()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await s.App.Sender.Send(new SubmitComplaintCommand(order.Id, "DAMAGED_CARGO", null, null, 100_000, TestPayout.Valid));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(("DAMAGED_CARGO", "Hàng bị hư hỏng"), (refund.ReasonCode, refund.Reason));
    }

    [Fact]
    public async Task Code_not_in_the_list_is_refused()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new SubmitComplaintCommand(order.Id, "CHANGED_PLAN", null, null, 100_000, TestPayout.Valid))); // a cancellation reason

        Assert.Contains("ReasonCode", error.Errors.Keys);
        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Code_switched_off_by_the_admin_is_refused()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        s.App.MasterData.Items.Single(i => i.Code == "DAMAGED_CARGO").SetActive(false);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new SubmitComplaintCommand(order.Id, "DAMAGED_CARGO", null, null, 100_000, TestPayout.Valid)));

        Assert.Empty(s.App.Refunds.Items);
    }

    [Theory]
    [InlineData("OTHER")]
    [InlineData("ÓTHER")] // the database column ignores case and accents
    public async Task Other_needs_a_written_reason(string code)
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new SubmitComplaintCommand(order.Id, code, " ", null, 100_000, TestPayout.Valid)));

        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Other_with_a_written_reason_is_accepted()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await s.App.Sender.Send(new SubmitComplaintCommand(order.Id, "OTHER", " Sai địa chỉ ", null, 100_000, TestPayout.Valid));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(("OTHER", "Sai địa chỉ"), (refund.ReasonCode, refund.Reason));
    }

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var s = new TripSetup(AccountStatus.Locked);
        var order = s.Delivered();

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(Complaint(order)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
    }

    [Fact]
    public async Task Signed_out_caller_is_unauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => new TestApp().Sender.Send(new SubmitComplaintCommand(1, "DAMAGED_CARGO", null, null, 1, TestPayout.Valid)));

    // ---- Incident refund (UC-CUS-17, 18) ----

    [Fact]
    public async Task Customer_asks_for_a_refund_for_an_incident_on_their_order_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var incident = AddIncident(s, order);

        var result = await s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ 3 giờ", [Photo], 50_000, TestPayout.Valid));

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(refund.Id, result.RefundId);
        Assert.Equal((RefundSourceType.Incident, (long?)incident.Id, order.Id), (refund.SourceType, refund.IncidentId, refund.OrderId));
        Assert.Equal("dispatchers", Assert.Single(s.App.Realtime.Sent).Target);
    }

    [Fact]
    public async Task One_open_refund_per_incident_B9()
    {
        var s = new TripSetup();
        var incident = AddIncident(s, s.PickedUp());
        await s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, TestPayout.Valid));

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, TestPayout.Valid)));
    }

    [Fact]
    public async Task Incident_refund_on_a_cash_order_is_refused_B9()
    {
        var s = new TripSetup();
        var incident = AddIncident(s, s.PickedUp(method: PaymentMethod.Cash));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, TestPayout.Valid)));

        Assert.Equal("REFUND_NOT_ALLOWED", error.Code);
        Assert.Empty(s.App.Refunds.Items);
    }

    // ---- The refunds of one order add up to at most its total (B9); this order's total is 180,000 ----

    [Fact]
    public async Task Two_incidents_cannot_claim_more_than_the_order_total_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var first = AddIncident(s, order);
        var second = AddIncident(s, order);
        await s.App.Sender.Send(new RequestIncidentRefundCommand(first.Id, "Trễ", null, 100_000, TestPayout.Valid));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RequestIncidentRefundCommand(second.Id, "Vỡ", null, 80_001, TestPayout.Valid)));

        Assert.Equal("REFUND_TOTAL_EXCEEDED", error.Code);
        Assert.Contains("80000", error.Message); // what is left to ask for
        Assert.Single(s.App.Refunds.Items);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task What_is_left_of_the_total_can_still_be_claimed_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var first = AddIncident(s, order);
        var second = AddIncident(s, order);
        await s.App.Sender.Send(new RequestIncidentRefundCommand(first.Id, "Trễ", null, 100_000, TestPayout.Valid));

        await s.App.Sender.Send(new RequestIncidentRefundCommand(second.Id, "Vỡ", null, 80_000, TestPayout.Valid));

        Assert.Equal(2, s.App.Refunds.Items.Count);
    }

    [Fact]
    public async Task Complaint_counts_what_incidents_already_claimed_B9()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var incident = AddIncident(s, order);
        await s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 150_000, TestPayout.Valid));

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(Complaint(order, 30_001)));

        Assert.Equal("REFUND_TOTAL_EXCEEDED", error.Code);
        Assert.Single(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Nothing_more_can_be_claimed_once_the_order_was_refunded_in_full_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var incident = AddIncident(s, order);
        var paid = Refund.Request(order.Id, incident.Id, RefundSourceType.Cancellation, "Hủy", null, order.TotalFee);
        paid.Approve(1, TripSetup.Now);
        paid.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", TripSetup.Now);
        paid.MarkProcessed("BANK1", TripSetup.Now);
        s.App.Refunds.Add(paid);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 1, TestPayout.Valid)));

        Assert.Equal("REFUND_TOTAL_EXCEEDED", error.Code);
    }

    [Fact]
    public async Task A_lowered_proposal_counts_instead_of_what_was_asked_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var first = AddIncident(s, order);
        var second = AddIncident(s, order);
        await s.App.Sender.Send(new RequestIncidentRefundCommand(first.Id, "Trễ", null, 180_000, TestPayout.Valid));
        s.App.Refunds.Items[0].Propose(50_000, "Một phần", 1);

        await s.App.Sender.Send(new RequestIncidentRefundCommand(second.Id, "Vỡ", null, 130_000, TestPayout.Valid));

        Assert.Equal(2, s.App.Refunds.Items.Count);
    }

    [Theory]
    [InlineData("Withdrawn")]
    [InlineData("Rejected")]
    [InlineData("Failed")]
    public async Task Refunds_that_paid_nothing_do_not_count_B9(string outcome)
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var first = AddIncident(s, order);
        var second = AddIncident(s, order);
        await s.App.Sender.Send(new RequestIncidentRefundCommand(first.Id, "Trễ", null, 180_000, TestPayout.Valid));
        var refund = s.App.Refunds.Items[0];
        switch (outcome)
        {
            case "Withdrawn":
                refund.Withdraw();
                break;
            case "Rejected":
                refund.RejectRefund(1, TripSetup.Now);
                break;
            default:
                refund.Propose(180_000, "Đủ", 1);
                refund.Approve(1, TripSetup.Now);
                refund.MarkFailed("Sai số tài khoản");
                break;
        }

        await s.App.Sender.Send(new RequestIncidentRefundCommand(second.Id, "Vỡ", null, 180_000, TestPayout.Valid));

        Assert.Equal(2, s.App.Refunds.Items.Count);
    }

    [Fact]
    public async Task Incident_on_another_customers_order_is_refused_C10()
    {
        var s = new TripSetup();
        var incident = AddIncident(s, s.PickedUp(customerId: s.Other.Id));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, TestPayout.Valid)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Empty(s.App.Refunds.Items);
    }

    [Fact]
    public async Task Unknown_incident_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new TripSetup().App.Sender.Send(new RequestIncidentRefundCommand(999, "x", null, 1, TestPayout.Valid)));

    // ---- Reply to an information request (UC-CUS-22, DECISIONS 7.4) ----

    [Fact]
    public async Task Customer_answers_the_dispatcher_with_text_and_more_photos_7_4()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        await s.App.Sender.Send(Complaint(order));
        var refund = s.App.Refunds.Items[0];
        refund.RequestMoreInfo("Chụp thêm thùng hàng");
        s.App.Realtime.Sent.Clear();

        await s.App.Sender.Send(new ReplyToInfoRequestCommand(refund.Id, " Đây là ảnh thùng ", [Photo]));

        Assert.Equal("Đây là ảnh thùng", refund.CustomerReply);
        Assert.Equal(2, refund.EvidenceUrls!.Count);
        Assert.Equal("dispatchers", Assert.Single(s.App.Realtime.Sent).Target);
    }

    [Fact]
    public async Task Reply_without_a_request_is_refused()
    {
        var s = new TripSetup();
        await s.App.Sender.Send(Complaint(s.Delivered()));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReplyToInfoRequestCommand(s.App.Refunds.Items[0].Id, "ok", null)));

        Assert.Equal("NO_INFO_REQUEST", error.Code);
    }

    [Fact]
    public async Task Reply_to_another_customers_refund_is_refused_C10()
    {
        var s = new TripSetup();
        var order = s.Delivered(customerId: s.Other.Id);
        var refund = order.FileComplaint(s.Other.Id, "Hàng vỡ", null, 100_000, TripSetup.Now);
        s.App.Refunds.Add(refund);
        refund.RequestMoreInfo("Thêm ảnh");

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ReplyToInfoRequestCommand(refund.Id, "ok", null)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Null(refund.CustomerReply);
    }

    [Fact]
    public async Task Reply_is_required() =>
        await Assert.ThrowsAsync<ValidationException>(() => new TripSetup().App.Sender.Send(new ReplyToInfoRequestCommand(1, " ", null)));

    // ---- Withdraw (UC-CUS-24) ----

    [Fact]
    public async Task Customer_withdraws_a_complaint_under_review()
    {
        var s = new TripSetup();
        await s.App.Sender.Send(Complaint(s.Delivered()));
        var refund = s.App.Refunds.Items[0];

        await s.App.Sender.Send(new WithdrawRefundCommand(refund.Id));

        Assert.Equal(RefundStatus.Withdrawn, refund.RefundStatus);
        Assert.Equal(2, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Another_customers_refund_cannot_be_withdrawn_C10()
    {
        var s = new TripSetup();
        var order = s.Delivered(customerId: s.Other.Id);
        var refund = order.FileComplaint(s.Other.Id, "Hàng vỡ", null, 100_000, TripSetup.Now);
        s.App.Refunds.Add(refund);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new WithdrawRefundCommand(refund.Id)));

        Assert.Equal("ORDER_NOT_YOURS", error.Code);
        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
    }

    [Fact]
    public async Task A_cancellation_refund_cannot_be_withdrawn_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var refund = Refund.Request(order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, order.TotalFee);
        s.App.Refunds.Add(refund);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new WithdrawRefundCommand(refund.Id)));

        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_new_incident_refund_is_allowed_after_the_first_was_withdrawn()
    {
        var s = new TripSetup();
        var incident = AddIncident(s, s.PickedUp());
        await s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 50_000, TestPayout.Valid));
        await s.App.Sender.Send(new WithdrawRefundCommand(s.App.Refunds.Items[0].Id));

        await s.App.Sender.Send(new RequestIncidentRefundCommand(incident.Id, "Trễ", null, 40_000, TestPayout.Valid));

        Assert.Equal(2, s.App.Refunds.Items.Count);
    }

    [Fact]
    public async Task Amount_is_a_whole_number_of_dong()
    {
        var s = new TripSetup();
        var order = s.Delivered();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(Complaint(order, amount: 100_000.5m)));
    }

    [Fact]
    public async Task Unknown_refund_is_not_found() =>
        await Assert.ThrowsAsync<NotFoundException>(() => new TripSetup().App.Sender.Send(new WithdrawRefundCommand(999)));
}
