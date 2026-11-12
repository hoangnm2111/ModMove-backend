using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Incidents.Commands.CancelIncident;
using ModMove.Application.Features.Incidents.Commands.CancelOrderByIncident;
using ModMove.Application.Features.Incidents.Commands.ReassignIncidentDriver;
using ModMove.Application.Features.Incidents.Commands.ReleaseIncident;
using ModMove.Application.Features.Incidents.Commands.ResolveIncidentContinue;
using ModMove.Application.Features.Incidents.Commands.StartIncidentHandling;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// UC-DIS-05, UC-DIS-06, UC-DIS-07, UC-DIS-10: Dispatcher incident handling, driver reassignment,
/// order cancellation due to incident, and incident cancellation through the real MediatR pipeline.
/// </summary>
public class IncidentResolutionTests
{
    [Fact]
    public async Task StartHandling_MovesOpenIncidentToInProgress_AndAssignsDispatcher()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);

        await s.App.Sender.Send(new StartIncidentHandlingCommand(incident.Id));

        Assert.Equal(IncidentResolutionStatus.InProgress, incident.ResolutionStatus);
        Assert.Equal(s.Dispatcher.Id, incident.HandledBy);
        Assert.Equal(1, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task StartHandling_tells_the_driver_and_the_customer_once_8_3()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);

        await s.App.Sender.Send(new StartIncidentHandlingCommand(incident.Id));
        await s.App.Sender.Send(new StartIncidentHandlingCommand(incident.Id)); // same Dispatcher again: nothing new to say

        var toCustomer = Assert.Single(s.SentTo(s.Customer));
        Assert.Equal("INCIDENT_IN_PROGRESS", toCustomer.Type);
        Assert.Equal(incident.Id.ToString(), toCustomer.Data!["incidentId"]);
        Assert.Equal(order.Id.ToString(), toCustomer.Data["orderId"]);

        var toDriver = Assert.Single(s.SentTo(s.Driver));
        Assert.Equal("INCIDENT_IN_PROGRESS", toDriver.Type);
        Assert.Equal(incident.Id.ToString(), toDriver.Data!["incidentId"]);
    }

    [Fact]
    public async Task StartHandling_a_customer_incident_keeps_the_customers_words_from_the_driver_8_3()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = InTransitIncident.Report(order.Id, s.Customer.Id, IncidentType.Other, "Tài xế lái ẩu", null, null, null);
        s.App.Incidents.Add(incident);

        await s.App.Sender.Send(new StartIncidentHandlingCommand(incident.Id));

        var toDriver = Assert.Single(s.SentTo(s.Driver));
        Assert.Equal("INCIDENT_IN_PROGRESS", toDriver.Type);
        Assert.DoesNotContain("lái ẩu", toDriver.Body);
        Assert.Single(s.SentTo(s.Customer), n => n.Type == "INCIDENT_IN_PROGRESS");
    }

    [Fact]
    public async Task StartHandling_an_incident_left_by_the_previous_driver_tells_that_driver_not_the_new_one_8_3()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var first = s.Incident(order);
        var leftOpen = s.Incident(order, IncidentType.Other);
        var (newDriverUser, newProfile, _) = s.ReplacementDriver();
        order.ReassignDriver(newProfile.Id, s.Dispatcher.Id, "Đổi xe");
        first.Resolve(IncidentDecision.ReassignDriver, "Đổi xe", s.Dispatcher.Id, DispatcherSetup.Now);

        await s.App.Sender.Send(new StartIncidentHandlingCommand(leftOpen.Id));

        Assert.Single(s.SentTo(s.Driver), n => n.Type == "INCIDENT_IN_PROGRESS");
        Assert.DoesNotContain(s.SentTo(newDriverUser), n => n.Type == "INCIDENT_IN_PROGRESS");
        Assert.Single(s.SentTo(s.Customer), n => n.Type == "INCIDENT_IN_PROGRESS");
    }

    [Fact]
    public async Task StartHandling_AlreadyHandledByAnotherDispatcher_Throws409Conflict()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        var otherDispatcher = User.Create("Điều phối 2", "0900000099", "disp2@x.vn", "h", UserRole.Dispatcher, emailVerified: true);
        s.App.Users.Add(otherDispatcher);
        incident.StartHandling(otherDispatcher.Id);

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new StartIncidentHandlingCommand(incident.Id)));
    }

    [Fact]
    public async Task ResolveContinue_FromOpenOrOwnInProgress_ResolvesIncident_KeepsOrderStatus_AndNotifiesBothParties()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);

        await s.App.Sender.Send(new ResolveIncidentContinueCommand(incident.Id, "  Xe đã sửa xong, tiếp tục giao  "));

        Assert.Equal(IncidentResolutionStatus.Resolved, incident.ResolutionStatus);
        Assert.Equal(IncidentDecision.ContinueTrip, incident.ResolutionDecision);
        Assert.Equal("Xe đã sửa xong, tiếp tục giao", incident.ResolutionNote);
        Assert.Equal(s.Dispatcher.Id, incident.HandledBy);
        Assert.Equal(DispatcherSetup.Now, incident.ResolvedAt);
        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);

        var toCustomer = Assert.Single(s.SentTo(s.Customer));
        Assert.Equal("INCIDENT_RESOLVED", toCustomer.Type);
        Assert.Contains("Xe đã sửa xong, tiếp tục giao", toCustomer.Body);

        var toDriver = Assert.Single(s.SentTo(s.Driver));
        Assert.Equal("INCIDENT_DECIDED", toDriver.Type);
        Assert.Equal("ContinueTrip", toDriver.Data!["decision"]);
    }

    [Fact]
    public async Task ResolveContinue_WhenHeldByAnotherDispatcher_Throws409Conflict()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        incident.StartHandling(dispatcherId: 999);

        await Assert.ThrowsAsync<ConflictException>(() =>
            s.App.Sender.Send(new ResolveIncidentContinueCommand(incident.Id, "Tiếp tục")));
    }

    [Fact]
    public async Task ReassignDriver_EligibleReplacement_MovesOrderBackToAccepted_ResetsPickupProof_AndNotifiesCustomerAndBothDrivers()
    {
        var s = new DispatcherSetup();
        var order = s.PickedUp();
        order.RejectPickupEvidence("Ảnh mờ", s.Customer.Id);
        var incident = s.Incident(order);
        var (newDriverUser, newProfile, _) = s.ReplacementDriver();

        await s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, newProfile.Id, "  Đổi xe thay thế  "));

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
        Assert.Equal(newProfile.Id, order.DriverProfileId);
        Assert.Equal(0, order.Proof!.PickupSubmissionCount);
        Assert.Equal(IncidentResolutionStatus.Resolved, incident.ResolutionStatus);
        Assert.Equal(IncidentDecision.ReassignDriver, incident.ResolutionDecision);
        Assert.Equal("Đổi xe thay thế", incident.ResolutionNote);

        // Customer notified
        var toCustomer = Assert.Single(s.SentTo(s.Customer));
        Assert.Equal("INCIDENT_RESOLVED", toCustomer.Type);

        // Both old driver and new driver receive INCIDENT_DECIDED AND OrderStatusChanged (TEAM-PLAN L102, B12)
        Assert.Single(s.SentTo(s.Driver), n => n.Type == "INCIDENT_DECIDED");
        Assert.Single(s.SentTo(newDriverUser), n => n.Type == "INCIDENT_DECIDED");

        var statusToUsers = s.App.Realtime.Sent
            .Where(x => x is { Target: "user", EventName: RealtimeEvents.OrderStatusChanged })
            .Select(x => x.Id)
            .ToList();
        Assert.Contains(s.Driver.Id.ToString(), statusToUsers);
        Assert.Contains(newDriverUser.Id.ToString(), statusToUsers);
    }

    [Fact]
    public async Task Reassign_refuses_a_driver_whose_gps_is_exactly_5_minutes_old_6_4()
    {
        var s = new DispatcherSetup();
        var incident = s.Incident(s.InTransit(PaymentMethod.Cash));
        var (_, atFiveMinutes, _) = s.ReplacementDriver("3", locationAt: DispatcherSetup.Now.AddMinutes(-5));

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, atFiveMinutes.Id, "Đổi tài xế")));

        Assert.Equal("DRIVER_NOT_ELIGIBLE", error.Code);
    }

    [Fact]
    public async Task ReassignDriver_RejectsStaleGps_WrongVehicleType_OutsideRadius_BusyDriver_OrLowCodDeposit()
    {
        var s = new DispatcherSetup();
        var cashOrder = s.InTransit(PaymentMethod.Cash);
        var incident = s.Incident(cashOrder);

        // 1. Stale GPS (> 5 minutes, DECISIONS 6.4)
        var (_, staleGps, _) = s.ReplacementDriver("3", locationAt: DispatcherSetup.Now.AddMinutes(-6));
        var ex1 = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, staleGps.Id, "Đổi tài xế")));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex1.Code);

        // 2. Wrong vehicle type (DECISIONS B5, 7.2)
        var (_, wrongType, _) = s.ReplacementDriver("4", vehicleTypeId: 2);
        var ex2 = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, wrongType.Id, "Đổi tài xế")));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex2.Code);

        // 3. Outside 5 km radius
        var (_, farAway, _) = s.ReplacementDriver("5", latitude: 21.20m, longitude: 105.99m);
        var ex3 = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, farAway.Id, "Đổi tài xế")));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex3.Code);

        // 4. Busy with another active order (GB-09)
        var (busyUser, busyProfile, _) = s.ReplacementDriver("6");
        var otherOrder = ShippingOrder.Create(
            "MM20261020099", s.Customer.Id, vehicleTypeId: 1, couponId: null,
            "A", 21.0m, 105.8m, "B", 21.1m, 105.9m, "Bình", "0912345678",
            5m, 15, false, 100_000, 0, 0, PaymentMethod.Cash, PayerRole.Sender,
            CargoDetail.Create("BOX", 1, 10, 30, 30, 30, ["a.jpg"], null), DispatcherSetup.Now);
        otherOrder.Accept(busyProfile.Id, busyUser.Id, DispatcherSetup.Now);
        s.App.Orders.Add(otherOrder);
        var ex4 = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, busyProfile.Id, "Đổi tài xế")));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex4.Code);

        // 5. Insufficient COD deposit (< 200,000 VND for cash order, GB-16)
        var (_, lowDeposit, _) = s.ReplacementDriver("7", deposit: 100_000);
        var ex5 = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, lowDeposit.Id, "Đổi tài xế")));
        Assert.Equal("DRIVER_NOT_ELIGIBLE", ex5.Code);
    }

    [Fact]
    public async Task CancelOrderByIncident_InTransitWithoutCargoDisposition_ThrowsCargoDispositionRequired()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incident = s.Incident(order);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new CancelOrderByIncidentCommand(incident.Id, "Không có xe thay thế", Refund: true, CargoDisposition: null)));

        Assert.Equal("CARGO_DISPOSITION_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task CancelOrderByIncident_PaidPayOsMidTrip_CancelsOrder_RecordsDisposition_AndCreatesFullCancellationRefund()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incident = s.Incident(order);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incident.Id, "Xe hỏng nặng không có xe thay", Refund: true, CargoDisposition.ReturnToPickup));

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        Assert.Contains("ReturnToPickup", order.CancellationReason);
        Assert.Equal(IncidentResolutionStatus.Resolved, incident.ResolutionStatus);
        Assert.Equal(IncidentDecision.CancelOrder, incident.ResolutionDecision);
        Assert.Contains("ReturnToPickup", incident.ResolutionNote);

        var refund = Assert.Single(s.App.Refunds.Items);
        Assert.Equal(RefundSourceType.Cancellation, refund.SourceType);
        Assert.Equal(incident.Id, refund.IncidentId);
        Assert.Equal(order.TotalFee, refund.RequestedAmount);
        Assert.Equal(order.TotalFee, refund.ProposedAmount);

        Assert.Single(s.SentTo(s.Customer), n => n.Type == "INCIDENT_RESOLVED");
        Assert.Single(s.SentTo(s.Driver), n => n.Type == "INCIDENT_DECIDED");
    }

    [Fact]
    public async Task CancelOrderByIncident_CashOrderRequestingRefund_ThrowsRefundNotAllowed()
    {
        var s = new DispatcherSetup();
        var cashOrder = s.Arrived(PaymentMethod.Cash);
        var incident = s.Incident(cashOrder, IncidentType.Other);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new CancelOrderByIncidentCommand(incident.Id, "Sự cố", Refund: true)));

        Assert.Equal("REFUND_NOT_ALLOWED", ex.Code);
    }

    [Fact]
    public async Task CancelOrderByIncident_CustomerFaultNoRefund_ProposesZeroOnExistingRequestedIncidentRefund()
    {
        var s = new DispatcherSetup();
        var order = s.Arrived(PaymentMethod.PayOS);
        var incident = s.Incident(order, IncidentType.Other);
        var customerRefund = s.IncidentRefund(order, incident, requested: 100_000);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(incident.Id, "Hàng cấm tại điểm lấy", Refund: false));

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        Assert.Single(s.App.Refunds.Items);
        Assert.Equal(0m, customerRefund.ProposedAmount);
        Assert.Equal(s.Dispatcher.Id, customerRefund.ProposedBy);
    }

    [Fact]
    public async Task CancelIncident_CancelsIncident_ProposesZeroOnOpenRequestedIncidentRefund_AndNotifiesCustomerAndDriver()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incident = s.Incident(order, IncidentType.Other);
        var openRefund = s.IncidentRefund(order, incident, requested: 80_000);

        await s.App.Sender.Send(new CancelIncidentCommand(incident.Id, "  Báo cáo nhầm  "));

        Assert.Equal(IncidentResolutionStatus.Cancelled, incident.ResolutionStatus);
        Assert.Equal("Báo cáo nhầm", incident.ResolutionNote);
        Assert.Equal(s.Dispatcher.Id, incident.HandledBy);
        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);

        Assert.Equal(0m, openRefund.ProposedAmount);
        Assert.Equal(s.Dispatcher.Id, openRefund.ProposedBy);
        Assert.Contains("Báo cáo nhầm", openRefund.ProposalReason);

        Assert.Single(s.SentTo(s.Customer), n => n.Type == "INCIDENT_RESOLVED");
        Assert.Single(s.SentTo(s.Driver), n => n.Type == "INCIDENT_DECIDED");
    }

    [Fact]
    public async Task CancelOrderByIncident_WithMultipleOpenIncidents_AutoCancelsRemainingOpenIncidents_WithoutDuplicateOrderCancelledNotifications()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incidentA = s.Incident(order, IncidentType.VehicleBreakdown);
        var incidentB = s.Incident(order, IncidentType.DamagedCargo);
        var incidentC = s.Incident(order, IncidentType.Other);
        incidentC.CancelIncident("Đã hủy trước", s.Dispatcher.Id, DispatcherSetup.Now);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incidentA.Id, "Xe hỏng nặng", Refund: true, CargoDisposition.ReturnToPickup));

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        Assert.Equal(IncidentResolutionStatus.Resolved, incidentA.ResolutionStatus);
        Assert.Equal(IncidentDecision.CancelOrder, incidentA.ResolutionDecision);

        Assert.Equal(IncidentResolutionStatus.Cancelled, incidentB.ResolutionStatus);
        Assert.Equal($"Đơn đã hủy theo sự cố #{incidentA.Id}", incidentB.ResolutionNote);
        Assert.Equal(s.Dispatcher.Id, incidentB.HandledBy);

        Assert.Equal("Đã hủy trước", incidentC.ResolutionNote);
        Assert.Single(s.SentTo(s.Customer), n => n.Type == "INCIDENT_RESOLVED");
    }

    [Fact]
    public async Task CancelOrderByIncident_WithOpenRequestedIncidentRefundOnOtherIncident_ProposesZeroOnAllRequested_AndCreatesRefundForRemaining()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incidentA = s.Incident(order, IncidentType.VehicleBreakdown);
        var incidentB = s.Incident(order, IncidentType.DamagedCargo);
        var openRefundB = s.IncidentRefund(order, incidentB, requested: 180_000);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incidentA.Id, "Xe hỏng nặng", Refund: true, CargoDisposition.ReturnToPickup));

        Assert.Equal(0m, openRefundB.ProposedAmount);
        Assert.Equal(s.Dispatcher.Id, openRefundB.ProposedBy);

        var cancellationRefund = s.App.Refunds.Items.FirstOrDefault(r => r.SourceType == RefundSourceType.Cancellation);
        Assert.NotNull(cancellationRefund);
        Assert.Equal(180_000m, cancellationRefund.RequestedAmount);
        Assert.Equal(180_000m, cancellationRefund.ProposedAmount);
    }

    [Fact]
    public async Task CancelOrderByIncident_WhenPriorRefundAlreadyApproved_OnlyRefundsRemainingAmount()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incidentA = s.Incident(order, IncidentType.VehicleBreakdown);
        var incidentB = s.Incident(order, IncidentType.DamagedCargo);
        var approvedRefundB = s.IncidentRefund(order, incidentB, requested: 50_000);
        approvedRefundB.Propose(50_000m, "Duyệt 50k", s.Dispatcher.Id);
        approvedRefundB.Approve(99L, DispatcherSetup.Now);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incidentA.Id, "Xe hỏng", Refund: true, CargoDisposition.ReturnToPickup));

        var cancellationRefund = s.App.Refunds.Items.FirstOrDefault(r => r.SourceType == RefundSourceType.Cancellation);
        Assert.NotNull(cancellationRefund);
        Assert.Equal(130_000m, cancellationRefund.RequestedAmount);
        Assert.Equal(130_000m, cancellationRefund.ProposedAmount);

        var notification = s.SentTo(s.Customer).First(n => n.Type == "INCIDENT_RESOLVED");
        Assert.Contains("130", notification.Body);
        Assert.Contains("phần cước còn lại", notification.Body);
    }

    [Fact]
    public async Task CancelOrderByIncident_WithRefund_AsksTheCustomerForABankAccount_R01()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incident = s.Incident(order, IncidentType.VehicleBreakdown);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incident.Id, "Xe hỏng nặng", Refund: true, CargoDisposition.ReturnToPickup));

        var ask = Assert.Single(s.SentTo(s.Customer), n => n.Type == "REFUND_BANK_INFO_REQUIRED");
        Assert.Equal(order.Id.ToString(), ask.Data!["orderId"]);
    }

    [Fact]
    public async Task CancelOrderByIncident_WithoutRefund_DoesNotAskForABankAccount_R01()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incident = s.Incident(order, IncidentType.VehicleBreakdown);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incident.Id, "Hàng cấm", Refund: false, CargoDisposition.ReturnToPickup));

        Assert.DoesNotContain(s.SentTo(s.Customer), n => n.Type == "REFUND_BANK_INFO_REQUIRED");
    }

    [Fact]
    public async Task CancelOrderByIncident_WhenPriorRefundCoveredEntireTotal_CancelsOrderWithoutCreatingNewRefund()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit(PaymentMethod.PayOS);
        var incidentA = s.Incident(order, IncidentType.VehicleBreakdown);
        var incidentB = s.Incident(order, IncidentType.DamagedCargo);
        var approvedRefundB = s.IncidentRefund(order, incidentB, requested: 180_000);
        approvedRefundB.Propose(180_000m, "Duyệt toàn bộ", s.Dispatcher.Id);
        approvedRefundB.Approve(99L, DispatcherSetup.Now);

        await s.App.Sender.Send(new CancelOrderByIncidentCommand(
            incidentA.Id, "Xe hỏng", Refund: true, CargoDisposition.ReturnToPickup));

        Assert.Equal(OrderStatus.Cancelled, order.OrderStatus);
        Assert.Null(s.App.Refunds.Items.FirstOrDefault(r => r.SourceType == RefundSourceType.Cancellation));

        var notification = s.SentTo(s.Customer).First(n => n.Type == "INCIDENT_RESOLVED");
        Assert.Contains("toàn bộ tiền cước đã được duyệt hoàn trước đó", notification.Body);
    }

    [Theory]
    [InlineData("start")]
    [InlineData("resolve")]
    [InlineData("cancel")]
    public async Task A_decision_racing_another_dispatcher_is_a_409_and_tells_nobody(string step)
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        s.App.UnitOfWork.ThrowConflictOnSave = true; // the other Dispatcher's save moved resolution_status first

        await Assert.ThrowsAsync<ConflictException>(() => step switch
        {
            "start" => s.App.Sender.Send(new StartIncidentHandlingCommand(incident.Id)),
            "resolve" => s.App.Sender.Send(new ResolveIncidentContinueCommand(incident.Id, "Tiếp tục")),
            _ => s.App.Sender.Send(new CancelIncidentCommand(incident.Id, "Báo nhầm")),
        });

        Assert.Empty(s.App.Realtime.Sent);
    }

    [Fact]
    public async Task Dispatcher_releases_an_incident_and_another_one_takes_it_Q10()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        incident.StartHandling(s.Dispatcher.Id);

        await s.App.Sender.Send(new ReleaseIncidentCommand(incident.Id));

        Assert.Equal(IncidentResolutionStatus.Open, incident.ResolutionStatus);
        Assert.Null(incident.HandledBy);
        var released = Assert.Single(s.App.Realtime.Sent, x => x.Target == "dispatchers");
        Assert.Equal("INCIDENT_RELEASED", ((Notification)released.Payload).Type);
        Assert.Empty(s.SentTo(s.Customer)); // for them it is still being handled
        Assert.Empty(s.SentTo(s.Driver));

        var other = User.Create("Điều phối 2", "0900000099", "disp2@x.vn", "h", UserRole.Dispatcher, emailVerified: true);
        incident.StartHandling(other.Id); // what the next Dispatcher's start does
        Assert.Equal(other.Id, incident.HandledBy);
    }

    [Fact]
    public async Task A_released_driver_incident_still_holds_the_trip_7_6()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var incident = s.Incident(order);
        incident.StartHandling(s.Dispatcher.Id);

        await s.App.Sender.Send(new ReleaseIncidentCommand(incident.Id));

        Assert.True(await s.App.Incidents.HasOpenReportedByAsync(order.Id, s.Driver.Id));
    }

    [Fact]
    public async Task A_dispatcher_cannot_release_an_incident_held_by_another_one_Q10()
    {
        var s = new DispatcherSetup();
        var incident = s.Incident(s.InTransit());
        incident.StartHandling(dispatcherId: 999);

        await Assert.ThrowsAsync<ConflictException>(() => s.App.Sender.Send(new ReleaseIncidentCommand(incident.Id)));

        Assert.Equal(999L, incident.HandledBy);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task Releasing_an_incident_nobody_holds_is_a_business_error_Q10()
    {
        var s = new DispatcherSetup();
        var incident = s.Incident(s.InTransit()); // still Open

        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ReleaseIncidentCommand(incident.Id)));
    }

    [Fact]
    public async Task Releasing_needs_a_valid_incident_id()
    {
        var s = new DispatcherSetup();

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(new ReleaseIncidentCommand(0)));
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new ReleaseIncidentCommand(999)));
    }

    /// <summary>The pickup photos were rejected after the 5th submission (DECISIONS 6.3).</summary>
    private static ShippingOrder PickupPhotosUsedUp(DispatcherSetup s)
    {
        var order = s.PickedUp(); // 1st submission
        for (var i = 2; i <= 5; i++)
        {
            order.RejectPickupEvidence("Ảnh mờ", s.Customer.Id);
            order.ConfirmPickup(["1.jpg", "2.jpg"], s.Driver.Id, DispatcherSetup.Now);
        }
        order.RejectPickupEvidence("Vẫn mờ", s.Customer.Id);
        return order;
    }

    [Fact]
    public async Task ResolveContinue_is_refused_once_the_pickup_photos_are_used_up_6_3()
    {
        var s = new DispatcherSetup();
        var order = PickupPhotosUsedUp(s);
        var incident = s.Incident(order, IncidentType.Other);

        var error = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ResolveIncidentContinueCommand(incident.Id, "Tiếp tục")));

        Assert.Equal("PICKUP_PHOTO_LIMIT", error.Code);
        Assert.True(incident.IsOpen);
        Assert.Equal(0, s.App.UnitOfWork.Saves);
    }

    [Fact]
    public async Task ResolveContinue_is_allowed_while_pickup_photos_can_still_be_sent_6_3()
    {
        var s = new DispatcherSetup();
        var order = s.PickedUp();
        order.RejectPickupEvidence("Ảnh mờ", s.Customer.Id);
        var incident = s.Incident(order, IncidentType.Other);

        await s.App.Sender.Send(new ResolveIncidentContinueCommand(incident.Id, "Tiếp tục"));

        Assert.Equal(IncidentResolutionStatus.Resolved, incident.ResolutionStatus);
    }

    [Fact]
    public async Task Reassign_still_works_once_the_pickup_photos_are_used_up_6_3()
    {
        var s = new DispatcherSetup();
        var order = PickupPhotosUsedUp(s);
        var incident = s.Incident(order, IncidentType.Other);
        var (_, newProfile, _) = s.ReplacementDriver();

        await s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, newProfile.Id, "Đổi tài xế"));

        Assert.Equal(OrderStatus.Accepted, order.OrderStatus);
    }

    [Fact]
    public async Task ReassignDriver_on_a_customer_incident_looks_near_the_vehicle_not_the_customer_7_2()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        s.Profile.UpdateLocation(21.05m, 105.85m, DispatcherSetup.Now);
        // The customer reports from Ho Chi Minh City while the vehicle is in Ha Noi.
        var incident = InTransitIncident.Report(order.Id, s.Customer.Id, IncidentType.Other, "Tài xế lái ẩu", null, 10.7769m, 106.7009m);
        s.App.Incidents.Add(incident);
        var (_, newProfile, _) = s.ReplacementDriver(); // next to the vehicle

        await s.App.Sender.Send(new ReassignIncidentDriverCommand(incident.Id, newProfile.Id, "Đổi tài xế"));

        Assert.Equal(newProfile.Id, order.DriverProfileId);
    }

    [Fact]
    public async Task ResolveContinue_on_an_incident_left_by_the_previous_driver_tells_that_driver_8_3()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var first = s.Incident(order);
        var leftOpen = s.Incident(order, IncidentType.Other);
        var (newDriverUser, newProfile, _) = s.ReplacementDriver();
        order.ReassignDriver(newProfile.Id, s.Dispatcher.Id, "Đổi xe");
        first.Resolve(IncidentDecision.ReassignDriver, "Đổi xe", s.Dispatcher.Id, DispatcherSetup.Now);

        await s.App.Sender.Send(new ResolveIncidentContinueCommand(leftOpen.Id, "Đã xử lý"));

        Assert.Single(s.SentTo(s.Driver), n => n.Type == "INCIDENT_DECIDED");
        Assert.DoesNotContain(s.SentTo(newDriverUser), n => n.Type == "INCIDENT_DECIDED");
    }

    [Fact]
    public async Task CancelIncident_left_by_the_previous_driver_tells_that_driver_8_3()
    {
        var s = new DispatcherSetup();
        var order = s.InTransit();
        var first = s.Incident(order);
        var leftOpen = s.Incident(order, IncidentType.Other);
        var (newDriverUser, newProfile, _) = s.ReplacementDriver();
        order.ReassignDriver(newProfile.Id, s.Dispatcher.Id, "Đổi xe");
        first.Resolve(IncidentDecision.ReassignDriver, "Đổi xe", s.Dispatcher.Id, DispatcherSetup.Now);

        await s.App.Sender.Send(new CancelIncidentCommand(leftOpen.Id, "Báo trùng"));

        Assert.Single(s.SentTo(s.Driver), n => n.Type == "INCIDENT_DECIDED");
        Assert.DoesNotContain(s.SentTo(newDriverUser), n => n.Type == "INCIDENT_DECIDED");
    }
}

