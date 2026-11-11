using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Commands.ConfirmReceipt;
using ModMove.Application.Features.Refunds;
using ModMove.Application.Features.Refunds.Commands.ApproveRefund;
using ModMove.Application.Features.Refunds.Commands.MarkRefundPayoutFailed;
using ModMove.Application.Features.Refunds.Commands.ProcessRefundPayout;
using ModMove.Application.Features.Refunds.Commands.RejectRefund;
using ModMove.Application.Features.Refunds.Commands.SetRefundPayoutAccount;
using ModMove.Application.Features.Refunds.Queries.GetAdminRefunds;
using ModMove.WebApi.Controllers.Admin;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ValidationException = ModMove.Application.Common.Exceptions.ValidationException;

namespace ModMove.IntegrationTests;

/// <summary>
/// Admin refund approval, rejection, and manual payout:
/// POST api/admin/refunds/{id}/approve, /reject, /payout (UC-ADM-18..20; DECISIONS B9, 3.3, 7.3, CUS-23, 8.3; GB-19, GB-27).
/// </summary>
public class AdminRefundsTests
{
    private static readonly DateTimeOffset Now = TestApp.Start;

    private sealed record Setup(TestApp App, User Admin, User Customer, User Dispatcher, ShippingOrder Order);

    private static Setup Build(AccountStatus adminStatus = AccountStatus.Active)
    {
        var admin = User.Create("Admin", "0900000001", "admin@x.vn", "h", UserRole.Administrator, emailVerified: true);
        admin.ChangeStatus(adminStatus);
        var customer = User.Create("Khách", "0900000002", "khach@x.vn", "h", UserRole.Customer, emailVerified: true);
        customer.SetFcmToken("fcm-customer-token");
        var dispatcher = User.Create("Điều phối", "0900000003", "dis@x.vn", "h", UserRole.Dispatcher, emailVerified: true);

        var app = TestApp.SignedInAs(admin, customer, dispatcher);

        // TotalFee = 200,000 - 20,000 = 180,000
        var order = ShippingOrder.Create(
            "MM20261020001", customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            12m, 30, hasLoadingService: false, distanceFee: 200_000, serviceFee: 0, discountAmount: 20_000,
            PaymentMethod.PayOS, payerRole: null,
            CargoDetail.Create("FURNITURE", 1, 50, 100, 50, 50, ["a.jpg"], null), Now);
        order.MarkPaid(Now);
        app.Orders.Add(order);

        return new Setup(app, admin, customer, dispatcher, order);
    }

    [Fact]
    public async Task Admin_approves_a_cancellation_refund_directly_without_dispatcher_and_notifies_customer_B9_3_3_8_3()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Đổi kế hoạch", null, 180_000m, "CHANGED_PLAN");
        s.App.Refunds.Add(refund);
        var beforeVersion = refund.RowVersion;
        var decidedTime = Now.AddMinutes(10);
        s.App.Time.Now = decidedTime;

        await s.App.Sender.Send(new ApproveRefundCommand(refund.Id, "  Đã kiểm tra giao dịch PayOS  "));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Equal(180_000m, refund.ProposedAmount);
        Assert.Equal(s.Admin.Id, refund.ApprovedBy);
        Assert.Equal(decidedTime, refund.DecidedAt);
        Assert.Equal("Đã kiểm tra giao dịch PayOS", refund.DecisionNote);
        Assert.Equal(beforeVersion + 1, refund.RowVersion);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var signalR = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", s.Customer.Id.ToString()), (signalR.Target, signalR.Id));
        var payload = Assert.IsType<Notification>(signalR.Payload);
        Assert.Equal("REFUND_DECIDED", payload.Type);
        Assert.Equal(s.Order.Id.ToString(), payload.Data!["orderId"]);
        Assert.Single(s.App.Push.Sent);
    }

    [Fact]
    public async Task Admin_approves_a_proposed_complaint_refund_with_optional_null_note_CUS23()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Complaint, "Trầy góc bàn", ["a.jpg"], 100_000m, "DAMAGED_CARGO");
        refund.Propose(60_000m, "Khách gửi ảnh trầy nhẹ, đề xuất hoàn 60k", s.Dispatcher.Id);
        s.App.Refunds.Add(refund);

        await s.App.Sender.Send(new ApproveRefundCommand(refund.Id, "   "));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Equal(60_000m, refund.ProposedAmount);
        Assert.Null(refund.DecisionNote);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Approving_an_incident_or_complaint_refund_before_dispatcher_proposal_is_refused()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Complaint, "Hàng vỡ", ["a.jpg"], 100_000m);
        s.App.Refunds.Add(refund);

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ApproveRefundCommand(refund.Id, "Duyệt")));

        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Approving_a_refund_when_total_refunds_exceed_order_total_is_refused_with_REFUND_TOTAL_EXCEEDED_B9()
    {
        var s = Build();
        // Earlier incident refund already processed for 120,000 VND
        var first = Refund.Request(s.Order.Id, incidentId: 1, RefundSourceType.Incident, "Sự cố 1", null, 120_000m);
        first.Propose(120_000m, "Hoàn 120k", s.Dispatcher.Id);
        first.Approve(s.Admin.Id, Now);
        first.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        first.MarkProcessed("FT001", Now);
        s.App.Refunds.Add(first);

        // Second refund proposed at 80,000 VND -> 120,000 + 80,000 = 200,000 > 180,000 (order.TotalFee)
        var second = Refund.Request(s.Order.Id, null, RefundSourceType.Complaint, "Khiếu nại thêm", null, 80_000m);
        second.Propose(80_000m, "Hoàn 80k", s.Dispatcher.Id);
        s.App.Refunds.Add(second);

        var error = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ApproveRefundCommand(second.Id)));

        Assert.Equal("REFUND_TOTAL_EXCEEDED", error.Code);
        Assert.Equal(RefundStatus.Requested, second.RefundStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Admin_rejects_a_refund_with_reason_and_notifies_customer_CUS23_8_3()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Complaint, "Hàng vỡ", ["a.jpg"], 100_000m);
        s.App.Refunds.Add(refund);
        var decidedTime = Now.AddMinutes(15);
        s.App.Time.Now = decidedTime;

        await s.App.Sender.Send(new RejectRefundCommand(refund.Id, "Ảnh POD lúc giao hàng nguyên vẹn"));

        Assert.Equal(RefundStatus.Rejected, refund.RefundStatus);
        Assert.Equal(s.Admin.Id, refund.ApprovedBy);
        Assert.Equal(decidedTime, refund.DecidedAt);
        Assert.Equal("Ảnh POD lúc giao hàng nguyên vẹn", refund.DecisionNote);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var signalR = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal("REFUND_DECIDED", Assert.IsType<Notification>(signalR.Payload).Type);
    }

    [Fact]
    public async Task Admin_records_manual_payout_reference_code_and_notifies_customer_GB27_8_3()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        refund.Approve(s.Admin.Id, Now, "Đồng ý hoàn 100%");
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        s.App.Refunds.Add(refund);
        var paidOutTime = Now.AddHours(2);
        s.App.Time.Now = paidOutTime;

        await s.App.Sender.Send(new ProcessRefundPayoutCommand(refund.Id, "  FT26293998877  "));

        Assert.Equal(RefundStatus.Processed, refund.RefundStatus);
        Assert.Equal("FT26293998877", refund.BankReferenceCode);
        Assert.Equal(paidOutTime, refund.ProcessedAt);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var signalR = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal("REFUND_DECIDED", Assert.IsType<Notification>(signalR.Payload).Type);
    }

    [Fact]
    public async Task Payout_without_a_customer_bank_account_is_refused_but_approval_is_not_R01()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        s.App.Refunds.Add(refund);

        await s.App.Sender.Send(new ApproveRefundCommand(refund.Id));
        var savesBeforePayout = s.App.UnitOfWork.Saves;
        var sentBeforePayout = s.App.Realtime.Sent.Count;
        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ProcessRefundPayoutCommand(refund.Id, "FT111")));

        Assert.Equal("REFUND_BANK_ACCOUNT_REQUIRED", error.Code);
        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Equal(savesBeforePayout, s.App.UnitOfWork.Saves);
        Assert.Equal(sentBeforePayout, s.App.Realtime.Sent.Count);

        // Once the customer supplies an account, the Administrator can pay it out.
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now.AddMinutes(5));
        await s.App.Sender.Send(new ProcessRefundPayoutCommand(refund.Id, "FT111"));

        Assert.Equal(RefundStatus.Processed, refund.RefundStatus);
        Assert.Equal("FT111", refund.BankReferenceCode);
    }

    [Fact]
    public async Task Payout_on_a_Failed_refund_before_customer_fixes_account_is_refused_R01()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        refund.Approve(s.Admin.Id, Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        refund.MarkFailed("Sai tên chủ tài khoản");
        s.App.Refunds.Add(refund);

        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ProcessRefundPayoutCommand(refund.Id, "FT111")));

        Assert.Equal(RefundStatus.Failed, refund.RefundStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Realtime.Sent);
    }

    // ---- Payout failed (K6, UC-ADM-18 5a, R-01) ----

    [Fact]
    public async Task Admin_marks_approved_refund_payout_failed_and_notifies_customer_R01()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        refund.Approve(s.Admin.Id, Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        s.App.Refunds.Add(refund);
        var beforeVersion = refund.RowVersion;

        await s.App.Sender.Send(new MarkRefundPayoutFailedCommand(refund.Id, "  Sai tên chủ tài khoản  "));

        Assert.Equal(RefundStatus.Failed, refund.RefundStatus);
        Assert.Equal("Sai tên chủ tài khoản", refund.PayoutFailureReason);
        Assert.Equal(beforeVersion + 1, refund.RowVersion);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        var signalR = Assert.Single(s.App.Realtime.Sent, x => x.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal(("user", s.Customer.Id.ToString()), (signalR.Target, signalR.Id));
        var payload = Assert.IsType<Notification>(signalR.Payload);
        Assert.Equal("REFUND_PAYOUT_FAILED", payload.Type);
        Assert.Equal(s.Order.Id.ToString(), payload.Data!["orderId"]);
        Assert.DoesNotContain("0123456789", payload.Body);
        Assert.DoesNotContain("Sai tên chủ tài khoản", payload.Body);

        var push = Assert.Single(s.App.Push.Sent);
        Assert.Equal("REFUND_PAYOUT_FAILED", push.Data!["type"]);
    }

    [Fact]
    public async Task Full_R01_lifecycle_payout_failed_then_customer_updates_account_then_admin_pays_out()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        s.App.Refunds.Add(refund);

        // 1. Admin approves
        await s.App.Sender.Send(new ApproveRefundCommand(refund.Id));
        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);

        // 2. Transfer fails -> Admin records payout-failed
        await s.App.Sender.Send(new MarkRefundPayoutFailedCommand(refund.Id, "Tài khoản không tồn tại"));
        Assert.Equal(RefundStatus.Failed, refund.RefundStatus);

        // 3. Customer signs in and provides a different valid account -> refund returns to Approved
        var customerApp = TestApp.SignedInAs(s.Customer);
        customerApp.Orders.Add(s.Order);
        customerApp.Refunds.Add(refund);
        await customerApp.Sender.Send(new SetRefundPayoutAccountCommand(
            refund.Id, new PayoutAccountInput(false, "970415", "9876543210", "NGUYEN VAN A")));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Null(refund.PayoutFailureReason);

        // 4. Admin retries manual payout -> Processed
        await s.App.Sender.Send(new ProcessRefundPayoutCommand(refund.Id, "FT26293000111"));
        Assert.Equal(RefundStatus.Processed, refund.RefundStatus);
        Assert.Equal("FT26293000111", refund.BankReferenceCode);
    }

    [Theory]
    [InlineData(RefundStatus.Requested)]
    [InlineData(RefundStatus.Processed)]
    [InlineData(RefundStatus.Rejected)]
    [InlineData(RefundStatus.Failed)]
    public async Task Payout_failed_on_non_approved_refund_is_refused_R01(RefundStatus status)
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        if (status is RefundStatus.Processed or RefundStatus.Failed)
        {
            refund.Approve(s.Admin.Id, Now);
            refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
            if (status == RefundStatus.Processed)
                refund.MarkProcessed("FT1", Now);
            else
                refund.MarkFailed("Lỗi cũ");
        }
        else if (status == RefundStatus.Rejected)
        {
            refund.RejectRefund(s.Admin.Id, Now);
        }
        s.App.Refunds.Add(refund);

        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new MarkRefundPayoutFailedCommand(refund.Id, "Lỗi chuyển khoản")));

        Assert.Equal(status, refund.RefundStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Payout_failed_concurrent_save_throws_ConflictException_409_R01()
    {
        var s = Build();
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        refund.Approve(s.Admin.Id, Now);
        s.App.Refunds.Add(refund);
        s.App.UnitOfWork.ThrowConflictOnSave = true;

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new MarkRefundPayoutFailedCommand(refund.Id, "Lỗi chuyển khoản")));

        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public void Payout_failed_endpoint_is_POST_on_Admin_RefundsController()
    {
        var action = typeof(RefundsController).GetMethod(nameof(RefundsController.PayoutFailed))!;

        Assert.Equal("{id:long}/payout-failed", Assert.Single(action.GetCustomAttributes<HttpPostAttribute>()).Template);
        Assert.Equal("ModMove.WebApi.Controllers.Admin", typeof(RefundsController).Namespace);
    }

    [Fact]
    public async Task Payout_on_unapproved_or_already_processed_refund_is_refused()
    {
        var s = Build();
        var requested = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        s.App.Refunds.Add(requested);

        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ProcessRefundPayoutCommand(requested.Id, "FT111")));

        requested.Approve(s.Admin.Id, Now);
        requested.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        await s.App.Sender.Send(new ProcessRefundPayoutCommand(requested.Id, "FT111"));

        // Second payout attempt on Processed refund fails
        await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ProcessRefundPayoutCommand(requested.Id, "FT222")));
        Assert.Equal("FT111", requested.BankReferenceCode);
    }

    [Fact]
    public async Task Closing_a_complaint_by_payout_or_rejection_unblocks_order_completion_7_3()
    {
        var s = Build();
        var driverUser = User.Create("Tài xế", "0900000009", "drv@x.vn", "h", UserRole.Driver, emailVerified: true);
        s.App.Users.Add(driverUser);
        var profile = DriverProfile.Create(driverUser.Id, "001", "B2", "f.jpg", "b.jpg");
        profile.Approve(Vehicle.Create(1, 1, "29A-12345", 1000, "200x150x150", "r.jpg", "i.jpg", "v.jpg"));
        s.App.DriverProfiles.Add(profile);
        var wallet = DriverWallet.Create(profile.Id);
        s.App.DriverWallets.Add(wallet);

        s.Order.Accept(profile.Id, driverUser.Id, Now);
        s.Order.ConfirmArrival(driverUser.Id, Now.AddMinutes(5));
        s.Order.ConfirmPickup(["1.jpg", "2.jpg"], driverUser.Id, Now.AddMinutes(10));
        s.Order.ApprovePickupEvidence(s.Customer.Id, Now.AddMinutes(12));
        s.Order.Deliver("pod.jpg", driverUser.Id, Now.AddMinutes(30));

        var complaint = s.Order.FileComplaint(s.Customer.Id, "Hàng móp nhẹ", ["a.jpg"], 50_000m, Now.AddMinutes(40));
        complaint.Propose(30_000m, "Hoàn 30k", s.Dispatcher.Id);
        s.App.Refunds.Add(complaint);

        // 1. Admin approves complaint -> still open (Approved), waiting for payout
        await s.App.Sender.Send(new ApproveRefundCommand(complaint.Id, "Đồng ý 30k"));
        Assert.True(await s.App.Refunds.HasOpenComplaintAsync(s.Order.Id));

        // 2. Admin pays out complaint -> now Processed (closed!); the customer gave the account when filing (R-01)
        complaint.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", Now);
        await s.App.Sender.Send(new ProcessRefundPayoutCommand(complaint.Id, "FT99999"));
        Assert.False(await s.App.Refunds.HasOpenComplaintAsync(s.Order.Id));
    }

    [Fact]
    public async Task Inactive_admin_account_cannot_approve_reject_or_payout_B8()
    {
        var s = Build(AccountStatus.Locked);
        var refund = Refund.Request(s.Order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, 180_000m);
        s.App.Refunds.Add(refund);

        var err1 = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new ApproveRefundCommand(refund.Id)));
        var err2 = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new RejectRefundCommand(refund.Id)));
        var err3 = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new ProcessRefundPayoutCommand(refund.Id, "FT1")));
        var err4 = await Assert.ThrowsAsync<ForbiddenAccessException>(() => s.App.Sender.Send(new MarkRefundPayoutFailedCommand(refund.Id, "Lỗi")));

        Assert.Equal(("ACCOUNT_INACTIVE", "ACCOUNT_INACTIVE", "ACCOUNT_INACTIVE", "ACCOUNT_INACTIVE"), (err1.Code, err2.Code, err3.Code, err4.Code));
    }

    [Fact]
    public async Task Unknown_refund_throws_NotFoundException()
    {
        var s = Build();

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new ApproveRefundCommand(999)));
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new RejectRefundCommand(999)));
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new ProcessRefundPayoutCommand(999, "FT1")));
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new MarkRefundPayoutFailedCommand(999, "Lỗi")));
    }

    [Fact]
    public async Task Invalid_inputs_throw_ValidationException()
    {
        var s = Build();
        var tooLongNote = new string('x', 1001);
        var tooLongRef = new string('R', 51);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ApproveRefundCommand(0)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ApproveRefundCommand(1, tooLongNote)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new RejectRefundCommand(0)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new RejectRefundCommand(1, tooLongNote)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProcessRefundPayoutCommand(0, "FT1")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProcessRefundPayoutCommand(1, "")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProcessRefundPayoutCommand(1, "   ")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProcessRefundPayoutCommand(1, tooLongRef)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new MarkRefundPayoutFailedCommand(0, "Lỗi")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new MarkRefundPayoutFailedCommand(1, "")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new MarkRefundPayoutFailedCommand(1, "   ")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new MarkRefundPayoutFailedCommand(1, tooLongNote)));

        var queryValidator = new GetAdminRefundsQueryValidator();
        Assert.False((await queryValidator.ValidateAsync(new GetAdminRefundsQuery(Page: 0))).IsValid);
        Assert.False((await queryValidator.ValidateAsync(new GetAdminRefundsQuery(PageSize: 101))).IsValid);
        Assert.True((await queryValidator.ValidateAsync(new GetAdminRefundsQuery(1, 20, RefundStatus.Requested, RefundSourceType.Cancellation))).IsValid);
    }
}

