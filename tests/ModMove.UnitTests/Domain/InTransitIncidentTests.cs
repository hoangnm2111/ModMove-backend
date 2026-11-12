using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.UnitTests.Domain;

public class InTransitIncidentTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 8, 0, 0, TimeSpan.Zero);

    private static InTransitIncident Open() =>
        InTransitIncident.Report(orderId: 1, reportedBy: 3, IncidentType.VehicleBreakdown, "Xe hỏng", null, null, null);

    [Fact]
    public void Release_puts_the_incident_back_to_Open_for_another_dispatcher_Q10()
    {
        var incident = Open();
        incident.StartHandling(dispatcherId: 9);

        incident.Release(9);

        Assert.Equal(IncidentResolutionStatus.Open, incident.ResolutionStatus);
        Assert.Null(incident.HandledBy);
    }

    [Fact]
    public void Only_an_incident_in_the_dispatchers_hands_can_be_released_Q10()
    {
        var open = Open();
        Assert.Throws<DomainException>(() => open.Release(9));

        var heldByOther = Open();
        heldByOther.StartHandling(8);
        Assert.Throws<DomainException>(() => heldByOther.Release(9));
        Assert.Equal(8L, heldByOther.HandledBy);

        var resolved = Open();
        resolved.StartHandling(9);
        resolved.Resolve(IncidentDecision.ContinueTrip, "Đi tiếp", 9, Now);
        Assert.Throws<DomainException>(() => resolved.Release(9));
    }

    [Fact]
    public void Customer_cancelling_the_order_closes_an_open_incident_without_a_dispatcher_7_6()
    {
        var incident = Open();

        incident.CloseBecauseOrderCancelled("Khách đã hủy đơn.", Now);

        Assert.Equal(IncidentResolutionStatus.Cancelled, incident.ResolutionStatus);
        Assert.Null(incident.HandledBy);
        Assert.Null(incident.ResolutionDecision);
        Assert.Equal("Khách đã hủy đơn.", incident.ResolutionNote);
        Assert.Equal(Now, incident.ResolvedAt);
    }

    [Fact]
    public void An_incident_being_handled_is_closed_too_and_forgets_its_dispatcher_7_6()
    {
        var incident = Open();
        incident.StartHandling(dispatcherId: 9);

        incident.CloseBecauseOrderCancelled("Khách đã hủy đơn.", Now);

        Assert.Equal(IncidentResolutionStatus.Cancelled, incident.ResolutionStatus);
        Assert.Null(incident.HandledBy);
    }

    [Fact]
    public void A_closed_incident_cannot_be_closed_again()
    {
        var incident = Open();
        incident.Resolve(IncidentDecision.ContinueTrip, "Đi tiếp", 9, Now);

        Assert.Throws<DomainException>(() => incident.CloseBecauseOrderCancelled("Khách đã hủy đơn.", Now));
    }

    [Theory]
    [InlineData(IncidentResolutionStatus.Open, true)]
    [InlineData(IncidentResolutionStatus.InProgress, true)]
    [InlineData(IncidentResolutionStatus.Resolved, false)]
    [InlineData(IncidentResolutionStatus.Cancelled, false)]
    public void IsOpen_covers_open_and_in_progress(IncidentResolutionStatus status, bool open)
    {
        var incident = Open();
        if (status == IncidentResolutionStatus.InProgress)
            incident.StartHandling(9);
        else if (status == IncidentResolutionStatus.Resolved)
            incident.Resolve(IncidentDecision.ContinueTrip, null, 9, Now);
        else if (status == IncidentResolutionStatus.Cancelled)
            incident.CancelIncident("Báo nhầm", 9, Now);

        Assert.Equal(open, incident.IsOpen);
    }
}
