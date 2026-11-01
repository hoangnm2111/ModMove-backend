using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution;
using ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Dispatcher actions on complaints (UC-DIS-14, 15, 08, 09, 17).</summary>
public class ComplaintCommandsTests
{
    [Fact]
    public async Task Request_more_info_records_note_saves_and_tells_the_customer_MSG60()
    {
        var s = new DispatcherSetup();
        var order = s.Delivered();
        var refund = s.Complaint(order);

        await s.App.Sender.Send(new RequestComplaintInfoCommand(refund.Id, "Xin thêm ảnh mặt đáy tủ"));

        Assert.Equal("Xin thêm ảnh mặt đáy tủ", refund.InfoRequestNote);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        var notification = Assert.Single(s.SentTo(s.Customer));
        Assert.Equal("REFUND_INFO_REQUESTED", notification.Type);
        Assert.Contains("Xin thêm ảnh mặt đáy tủ", notification.Body);
        Assert.Equal(order.Id.ToString(), notification.Data!["orderId"]);
        Assert.Equal(refund.Id.ToString(), notification.Data["refundId"]);
    }

    [Fact]
    public async Task Request_more_info_fails_when_complaint_not_found()
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<NotFoundException>(
            () => s.App.Sender.Send(new RequestComplaintInfoCommand(999, "Ghi chú")));
    }

    [Fact]
    public async Task Request_more_info_fails_when_refund_is_not_a_complaint()
    {
        var s = new DispatcherSetup();
        var refund = Refund.Request(10L, null, RefundSourceType.Cancellation, "Khách hủy", null, 100000m);
        s.App.Refunds.Add(refund);

        await Assert.ThrowsAsync<NotFoundException>(
            () => s.App.Sender.Send(new RequestComplaintInfoCommand(refund.Id, "Ghi chú")));
    }

    [Fact]
    public async Task Propose_resolution_records_amount_reason_and_dispatcher()
    {
        var s = new DispatcherSetup();
        var refund = s.Complaint(s.Delivered());

        await s.App.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 50000m, "Hoàn 50% tiền"));

        Assert.Equal(50000m, refund.ProposedAmount);
        Assert.Equal("Hoàn 50% tiền", refund.ProposalReason);
        Assert.Equal(s.Dispatcher.Id, refund.ProposedBy);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Propose_zero_amount_is_allowed_as_no_refund_decision()
    {
        var s = new DispatcherSetup();
        var refund = s.Complaint(s.Delivered());

        await s.App.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 0m, "Từ chối hoàn tiền"));

        Assert.Equal(0m, refund.ProposedAmount);
        Assert.Equal("Từ chối hoàn tiền", refund.ProposalReason);
        Assert.Equal(s.Dispatcher.Id, refund.ProposedBy);
    }

    [Fact]
    public async Task Propose_above_requested_amount_is_rejected_by_domain()
    {
        var s = new DispatcherSetup();
        var refund = s.Complaint(s.Delivered()); // requested 100,000

        await Assert.ThrowsAsync<DomainException>(
            () => s.App.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 150000m, "Quá tiền")));
    }

    [Fact]
    public async Task Propose_on_closed_refund_is_rejected()
    {
        var s = new DispatcherSetup();
        var refund = s.Complaint(s.Delivered());
        refund.Withdraw();

        await Assert.ThrowsAsync<DomainException>(
            () => s.App.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 50000m, "Lý do")));
    }

    [Fact]
    public async Task Locked_dispatcher_is_refused_with_403_account_inactive_B8()
    {
        var s = new DispatcherSetup();
        var refund = s.Complaint(s.Delivered());
        s.Dispatcher.ChangeStatus(AccountStatus.Locked);

        var err1 = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => s.App.Sender.Send(new RequestComplaintInfoCommand(refund.Id, "Ghi chú")));
        Assert.Equal("ACCOUNT_INACTIVE", err1.Code);

        var err2 = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => s.App.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 50_000m, "Lý do")));
        Assert.Equal("ACCOUNT_INACTIVE", err2.Code);
    }

    [Fact]
    public async Task Fractional_vnd_proposal_is_rejected_C5()
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<ValidationException>(
            () => s.App.Sender.Send(new ProposeComplaintResolutionCommand(1L, 100.5m, "Lý do")));
    }
}
