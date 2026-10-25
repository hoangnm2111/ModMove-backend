using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Refunds.Commands.ProposeComplaintResolution;
using ModMove.Application.Features.Refunds.Commands.RequestComplaintInfo;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>Tests for Dispatcher actions on complaints (UC-DIS-14, 15, 08, 09, 17).</summary>
public class ComplaintCommandsTests
{
    private static User DispatcherUser() =>
        User.Create("Dispatcher", "0900000002", "disp@x.vn", "hash", UserRole.Dispatcher, true);

    [Fact]
    public async Task Request_more_info_records_note_and_saves()
    {
        var app = TestApp.SignedInAs(DispatcherUser());
        var refund = Refund.Request(10L, null, RefundSourceType.Complaint, "Hỏng hàng", null, 100000m);
        app.Refunds.Add(refund);

        await app.Sender.Send(new RequestComplaintInfoCommand(refund.Id, "Xin thêm ảnh mặt đáy tủ"));

        Assert.Equal("Xin thêm ảnh mặt đáy tủ", refund.InfoRequestNote);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Request_more_info_fails_when_complaint_not_found()
    {
        var app = TestApp.SignedInAs(DispatcherUser());

        await Assert.ThrowsAsync<NotFoundException>(
            () => app.Sender.Send(new RequestComplaintInfoCommand(999, "Ghi chú")));
    }

    [Fact]
    public async Task Request_more_info_fails_when_refund_is_not_a_complaint()
    {
        var app = TestApp.SignedInAs(DispatcherUser());
        var refund = Refund.Request(10L, null, RefundSourceType.Cancellation, "Khách hủy", null, 100000m);
        app.Refunds.Add(refund);

        await Assert.ThrowsAsync<NotFoundException>(
            () => app.Sender.Send(new RequestComplaintInfoCommand(refund.Id, "Ghi chú")));
    }

    [Fact]
    public async Task Propose_resolution_records_amount_reason_and_dispatcher()
    {
        var dispatcher = DispatcherUser();
        var app = TestApp.SignedInAs(dispatcher);
        var refund = Refund.Request(10L, null, RefundSourceType.Complaint, "Hỏng hàng", null, 100000m);
        app.Refunds.Add(refund);

        await app.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 50000m, "Hoàn 50% tiền"));

        Assert.Equal(50000m, refund.ProposedAmount);
        Assert.Equal("Hoàn 50% tiền", refund.ProposalReason);
        Assert.Equal(dispatcher.Id, refund.ProposedBy);
        Assert.Equal(1, app.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Propose_zero_amount_is_allowed_as_no_refund_decision()
    {
        var dispatcher = DispatcherUser();
        var app = TestApp.SignedInAs(dispatcher);
        var refund = Refund.Request(10L, null, RefundSourceType.Complaint, "Thái độ", null, 100000m);
        app.Refunds.Add(refund);

        await app.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 0m, "Từ chối hoàn tiền"));

        Assert.Equal(0m, refund.ProposedAmount);
        Assert.Equal("Từ chối hoàn tiền", refund.ProposalReason);
        Assert.Equal(dispatcher.Id, refund.ProposedBy);
    }

    [Fact]
    public async Task Propose_above_requested_amount_is_rejected_by_domain()
    {
        var app = TestApp.SignedInAs(DispatcherUser());
        var refund = Refund.Request(10L, null, RefundSourceType.Complaint, "Hỏng hàng", null, 100000m);
        app.Refunds.Add(refund);

        await Assert.ThrowsAsync<DomainException>(
            () => app.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 150000m, "Quá tiền")));
    }

    [Fact]
    public async Task Propose_on_closed_refund_is_rejected()
    {
        var app = TestApp.SignedInAs(DispatcherUser());
        var refund = Refund.Request(10L, null, RefundSourceType.Complaint, "Hỏng hàng", null, 100000m);
        refund.Withdraw();
        app.Refunds.Add(refund);

        await Assert.ThrowsAsync<DomainException>(
            () => app.Sender.Send(new ProposeComplaintResolutionCommand(refund.Id, 50000m, "Lý do")));
    }
}
