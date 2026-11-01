using ModMove.Application.Common.Exceptions;
using ModMove.Application.Features.Refunds.Commands.ProposeIncidentRefundResolution;
using ModMove.Application.Features.Refunds.Commands.RequestIncidentRefundInfo;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// Dispatcher on a refund the customer asked for because of an incident (UC-CUS-17 to UC-DIS-08, 09, 14, 17; DECISIONS B9, 7.4).
/// </summary>
public class IncidentRefundTests
{
    private static (DispatcherSetup S, ShippingOrder Order, InTransitIncident Incident, Refund Refund) WithRefund()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        var refund = s.IncidentRefund(order, incident);
        return (s, order, incident, refund);
    }

    [Fact]
    public async Task Asking_for_more_information_records_the_note_and_tells_the_customer_MSG60()
    {
        var (s, order, incident, refund) = WithRefund();

        await s.App.Sender.Send(new RequestIncidentRefundInfoCommand(incident.Id, " Xin ảnh hàng bị hỏng "));

        Assert.Equal("Xin ảnh hàng bị hỏng", refund.InfoRequestNote);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
        var notification = Assert.Single(s.SentTo(s.Customer));
        Assert.Equal("REFUND_INFO_REQUESTED", notification.Type);
        Assert.Contains("Xin ảnh hàng bị hỏng", notification.Body);
        Assert.Equal(order.Id.ToString(), notification.Data!["orderId"]);
        Assert.Equal(refund.Id.ToString(), notification.Data["refundId"]);
    }

    [Fact]
    public async Task A_new_question_clears_the_previous_answer_7_4()
    {
        var (s, _, incident, refund) = WithRefund();
        refund.RequestMoreInfo("Câu hỏi 1");
        refund.ReplyToInfoRequest("Trả lời 1", null);

        await s.App.Sender.Send(new RequestIncidentRefundInfoCommand(incident.Id, "Câu hỏi 2"));

        Assert.Equal("Câu hỏi 2", refund.InfoRequestNote);
        Assert.Null(refund.CustomerReply);
    }

    [Fact]
    public async Task An_incident_without_an_open_refund_is_not_found()
    {
        var s = new DispatcherSetup();
        var incident = s.Incident(s.InTransit());

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new RequestIncidentRefundInfoCommand(incident.Id, "Ghi chú")));
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(incident.Id, 1, "Lý do")));
    }

    [Fact]
    public async Task A_withdrawn_refund_is_no_longer_open()
    {
        var (s, _, incident, refund) = WithRefund();
        refund.Withdraw();

        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new RequestIncidentRefundInfoCommand(incident.Id, "Ghi chú")));
    }

    [Fact]
    public async Task Proposing_records_amount_reason_and_dispatcher()
    {
        var (s, _, incident, refund) = WithRefund();

        await s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(incident.Id, 60_000, " Hoàn 60% "));

        Assert.Equal(60_000m, refund.ProposedAmount);
        Assert.Equal("Hoàn 60%", refund.ProposalReason);
        Assert.Equal(s.Dispatcher.Id, refund.ProposedBy);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Proposing_zero_means_no_refund()
    {
        var (s, _, incident, refund) = WithRefund();

        await s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(incident.Id, 0, "Lỗi do khách"));

        Assert.Equal(0m, refund.ProposedAmount);
    }

    [Fact]
    public async Task Proposing_above_the_requested_amount_is_refused()
    {
        var (s, _, incident, _) = WithRefund(); // requested 100,000

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(incident.Id, 100_001, "Quá")));
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task A_cancellation_refund_goes_straight_to_the_admin_B9()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        s.App.Refunds.Add(ModMove.Domain.Entities.Refund.Request(
            order.Id, incident.Id, ModMove.Domain.Enums.RefundSourceType.Cancellation, "Hủy do sự cố", null, 180_000));

        var error = await Assert.ThrowsAsync<DomainException>(
            () => s.App.Sender.Send(new RequestIncidentRefundInfoCommand(incident.Id, "Ghi chú")));

        Assert.Equal("REFUND_NOT_FOR_DISPATCHER", error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task The_incident_id_must_be_positive(long id)
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new RequestIncidentRefundInfoCommand(id, "Ghi chú")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(id, 1, "Lý do")));
    }

    [Fact]
    public async Task The_note_and_reason_are_required()
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new RequestIncidentRefundInfoCommand(1, " ")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(1, 1, " ")));
        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(1, 10.5m, "Lý do")));
    }

    [Fact]
    public async Task A_locked_dispatcher_cannot_act_on_incident_refunds_B8()
    {
        var (s, _, incident, _) = WithRefund();
        s.Dispatcher.ChangeStatus(AccountStatus.Locked);

        var err1 = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => s.App.Sender.Send(new RequestIncidentRefundInfoCommand(incident.Id, "Ghi chú")));
        Assert.Equal("ACCOUNT_INACTIVE", err1.Code);

        var err2 = await Assert.ThrowsAsync<ForbiddenAccessException>(
            () => s.App.Sender.Send(new ProposeIncidentRefundResolutionCommand(incident.Id, 50_000m, "Lý do")));
        Assert.Equal("ACCOUNT_INACTIVE", err2.Code);
    }
}

