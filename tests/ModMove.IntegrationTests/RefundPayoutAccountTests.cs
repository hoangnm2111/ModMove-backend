using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Refunds;
using ModMove.Application.Features.Refunds.Commands.SetRefundPayoutAccount;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;
using ModMove.WebApi.Controllers.Customer;

namespace ModMove.IntegrationTests;

/// <summary>PUT api/customer/refunds/{id}/payout-account (R-01): the customer gives or fixes the account that receives a refund.</summary>
public class RefundPayoutAccountTests
{
    private static readonly PayoutAccountInput Other = new(false, "970415", "9876543210", "TRAN THI B");

    private static Refund CancellationRefund(TripSetup s, long? customerId = null)
    {
        var order = s.PickedUp(customerId);
        var refund = Refund.Request(order.Id, null, RefundSourceType.Cancellation, "Khách hủy", null, order.TotalFee);
        s.App.Refunds.Add(refund);
        return refund;
    }

    [Fact]
    public async Task Customer_gives_the_account_of_a_cancellation_refund_that_is_still_Requested_R01()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s);
        s.App.Time.Now = TripSetup.Now.AddHours(1);

        await s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, TestPayout.Valid));

        Assert.Equal(RefundStatus.Requested, refund.RefundStatus);
        Assert.Equal(("970436", "Vietcombank", "0123456789", "NGUYEN VAN A"),
            (refund.PayoutBankCode, refund.PayoutBankName, refund.PayoutAccountNumber, refund.PayoutAccountHolder));
        Assert.Equal(TripSetup.Now.AddHours(1), refund.PayoutInfoUpdatedAt);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Customer_can_change_the_account_while_the_refund_is_Approved_R01()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s);
        refund.Approve(1, TripSetup.Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", TripSetup.Now);

        await s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, Other));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Equal(("970415", "VietinBank", "9876543210", "TRAN THI B"),
            (refund.PayoutBankCode, refund.PayoutBankName, refund.PayoutAccountNumber, refund.PayoutAccountHolder));
    }

    [Fact]
    public async Task A_Failed_refund_goes_back_to_Approved_and_forgets_the_failure_R01()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s);
        refund.Approve(1, TripSetup.Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", TripSetup.Now);
        refund.MarkFailed("Sai số tài khoản");

        await s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, Other));

        Assert.Equal(RefundStatus.Approved, refund.RefundStatus);
        Assert.Null(refund.PayoutFailureReason);
        Assert.Equal("9876543210", refund.PayoutAccountNumber);
    }

    // A Failed refund counts for nothing in the order's total and the "one open" checks (B9), so letting it
    // back to Approved must run those checks again.

    private static Refund FailedRefund(
        TripSetup s, ShippingOrder order, RefundSourceType source, long? incidentId, decimal amount)
    {
        var refund = Refund.Request(order.Id, incidentId, source, "Lý do", null, amount);
        if (source != RefundSourceType.Cancellation)
            refund.Propose(amount, "Đủ", 1);
        refund.Approve(1, TripSetup.Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", TripSetup.Now);
        refund.MarkFailed("Sai số tài khoản");
        s.App.Refunds.Add(refund);
        return refund;
    }

    [Fact]
    public async Task A_Failed_refund_cannot_come_back_once_other_refunds_fill_the_order_total_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp(); // total 180,000
        var failed = FailedRefund(s, order, RefundSourceType.Cancellation, null, order.TotalFee);
        s.App.Refunds.Add(Refund.Request(order.Id, null, RefundSourceType.Complaint, "Khiếu nại sau", null, 100_000));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(failed.Id, Other)));

        Assert.Equal("REFUND_TOTAL_EXCEEDED", error.Code);
        Assert.Equal(RefundStatus.Failed, failed.RefundStatus);
        Assert.Equal("0123456789", failed.PayoutAccountNumber);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_Failed_complaint_cannot_come_back_while_the_order_has_another_open_complaint_B9()
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var failed = FailedRefund(s, order, RefundSourceType.Complaint, null, 50_000);
        s.App.Refunds.Add(Refund.Request(order.Id, null, RefundSourceType.Complaint, "Khiếu nại mới", null, 40_000));

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(failed.Id, Other)));

        Assert.Equal(RefundStatus.Failed, failed.RefundStatus);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_Failed_incident_refund_cannot_come_back_while_the_incident_has_another_open_refund_B9()
    {
        var s = new TripSetup();
        var order = s.PickedUp();
        var failed = FailedRefund(s, order, RefundSourceType.Incident, incidentId: 7, 50_000);
        s.App.Refunds.Add(Refund.Request(order.Id, 7, RefundSourceType.Incident, "Yêu cầu mới", null, 40_000));

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(failed.Id, Other)));

        Assert.Equal(RefundStatus.Failed, failed.RefundStatus);
    }

    [Fact]
    public async Task Sending_the_same_account_again_does_not_reopen_a_Failed_refund_R01()
    {
        var s = new TripSetup();
        var failed = FailedRefund(s, s.PickedUp(), RefundSourceType.Cancellation, null, 180_000);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(failed.Id, TestPayout.Valid)));

        Assert.Equal("REFUND_PAYOUT_UNCHANGED", error.Code);
        Assert.Equal(RefundStatus.Failed, failed.RefundStatus);
        Assert.Equal("Sai số tài khoản", failed.PayoutFailureReason);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Theory]
    [InlineData(RefundStatus.Processed)]
    [InlineData(RefundStatus.Rejected)]
    [InlineData(RefundStatus.Withdrawn)]
    public async Task A_closed_refund_keeps_its_account_REFUND_PAYOUT_LOCKED_R01(RefundStatus status)
    {
        var s = new TripSetup();
        var order = s.Delivered();
        var refund = order.FileComplaint(s.Customer.Id, "Hàng vỡ", null, 100_000, TripSetup.Now);
        refund.SetPayoutAccount("970436", "Vietcombank", "0123456789", "NGUYEN VAN A", TripSetup.Now);
        s.App.Refunds.Add(refund);
        switch (status)
        {
            case RefundStatus.Withdrawn:
                refund.Withdraw();
                break;
            case RefundStatus.Rejected:
                refund.RejectRefund(1, TripSetup.Now);
                break;
            default:
                refund.Propose(100_000, "Đủ", 1);
                refund.Approve(1, TripSetup.Now);
                refund.MarkProcessed("FT1", TripSetup.Now);
                break;
        }

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, Other)));

        Assert.Equal("REFUND_PAYOUT_LOCKED", error.Code);
        Assert.Equal("0123456789", refund.PayoutAccountNumber);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Another_customers_refund_is_a_404_not_a_hint_that_it_exists_C10()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s, customerId: s.Other.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, TestPayout.Valid)));

        Assert.False(refund.HasPayoutAccount);
    }

    [Fact]
    public async Task Unknown_refund_is_a_404() =>
        await Assert.ThrowsAsync<NotFoundException>(() =>
            new TripSetup().App.Sender.Send(new SetRefundPayoutAccountCommand(999, TestPayout.Valid)));

    [Fact]
    public async Task Signed_out_caller_is_unauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            new TestApp().Sender.Send(new SetRefundPayoutAccountCommand(1, TestPayout.Valid)));

    [Fact]
    public async Task Locked_account_is_refused_B8()
    {
        var s = new TripSetup(AccountStatus.Locked);
        var refund = CancellationRefund(s);

        var error = await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, TestPayout.Valid)));

        Assert.Equal("ACCOUNT_INACTIVE", error.Code);
        Assert.False(refund.HasPayoutAccount);
    }

    [Fact]
    public async Task The_Administrator_saving_at_the_same_moment_makes_it_a_409()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s);
        s.App.UnitOfWork.ThrowConflictOnSave = true; // RowVersion moved under the customer

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, TestPayout.Valid)));
    }

    [Fact]
    public async Task A_bank_outside_the_BANK_list_is_a_400()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s);

        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, TestPayout.Valid with { BankCode = "999999" })));

        Assert.False(refund.HasPayoutAccount);
    }

    [Fact]
    public async Task Invalid_input_is_a_400()
    {
        var s = new TripSetup();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new SetRefundPayoutAccountCommand(0, TestPayout.Valid)));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new SetRefundPayoutAccountCommand(1, null!)));
        await Assert.ThrowsAsync<ValidationException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(1, TestPayout.Valid with { AccountNumber = "12ab" })));
    }

    [Fact]
    public async Task Reusing_the_PayOS_account_without_one_on_record_is_NO_SUGGESTED_PAYOUT_ACCOUNT()
    {
        var s = new TripSetup();
        var refund = CancellationRefund(s);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new SetRefundPayoutAccountCommand(refund.Id, new PayoutAccountInput(true, null, null, null))));

        Assert.Equal("NO_SUGGESTED_PAYOUT_ACCOUNT", error.Code);
    }

    [Fact]
    public void Banks_lookup_is_a_public_GET_of_the_BANK_category_H4()
    {
        var action = typeof(ModMove.WebApi.Controllers.Public.LookupsController).GetMethod(nameof(ModMove.WebApi.Controllers.Public.LookupsController.Banks))!;

        Assert.Equal("banks", Assert.Single(action.GetCustomAttributes<HttpGetAttribute>()).Template);
        Assert.Equal("BANK", ModMove.Application.Features.MasterData.Queries.GetPublicContent.PublicContentCategories.Bank);
    }

    [Fact]
    public void The_action_is_PUT_payout_account_in_the_Customer_folder_so_other_roles_get_403_D2()
    {
        var action = typeof(RefundsController).GetMethod(nameof(RefundsController.SetPayoutAccount))!;

        var put = Assert.Single(action.GetCustomAttributes<HttpPutAttribute>());
        Assert.Equal("{id:long}/payout-account", put.Template);
        Assert.Equal("ModMove.WebApi.Controllers.Customer", typeof(RefundsController).Namespace); // RoleConvention maps it to Customer only
        Assert.Empty(action.GetCustomAttributes<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>());
    }
}
