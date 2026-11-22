using System.Data;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Application.Features.Trips.Commands.CollectCash;
using ModMove.Application.Features.Trips.Commands.ConfirmArrival;
using ModMove.Application.Features.Trips.Commands.ConfirmPickup;
using ModMove.Application.Features.Trips.Commands.DeliverTrip;
using ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;
using ModMove.Application.Features.Trips.Commands.UploadDriverImage;
using ModMove.Application.Features.Trips.Queries.GetDriverTripDetail;
using ModMove.Application.Features.Trips.Queries.GetDriverTrips;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Domain.Exceptions;

namespace ModMove.IntegrationTests;

/// <summary>
/// Driver uploads + trip lifecycle: UC-DRV-11..17, GB-06, GB-11, GB-12, DECISIONS 2.7, 6.1, 6.2, 6.3, 7.2, 8.3, B12.
/// </summary>
public class DriverTripsTests
{
    private const string Photo1 = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/1.jpg";
    private const string Photo2 = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/2.jpg";
    private const string PodPhoto = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/pod.jpg";
    private static readonly byte[] JpgBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private sealed class Setup
    {
        public TestApp App { get; }
        public User Driver { get; }
        public User OtherDriver { get; }
        public User Customer { get; }
        public DriverProfile Profile { get; }
        public DriverProfile OtherProfile { get; }

        public Setup(AccountStatus accountStatus = AccountStatus.Active, bool approved = true)
        {
            Driver = User.Create("Nguyễn Văn Tài", "0912000001", "driver@x.vn", "h:x", UserRole.Driver, emailVerified: true);
            Driver.ChangeStatus(accountStatus);
            OtherDriver = User.Create("Trần Văn Khác", "0912000002", "other@x.vn", "h:x", UserRole.Driver, emailVerified: true);
            Customer = User.Create("Lê Thị Khách", "0909000001", "customer@x.vn", "h:x", UserRole.Customer, emailVerified: true);
            Customer.SetFcmToken("fcm-customer-1");

            App = TestApp.SignedInAs(Driver, OtherDriver, Customer);

            Profile = DriverProfile.Create(Driver.Id, Photo1, Photo2);
            App.DriverProfiles.Add(Profile);
            if (approved)
                Profile.Approve(Vehicle.Create(Profile.Id, 1, "51C-123.45", 1000, "200x150x150", Photo1, Photo1, Photo1));

            OtherProfile = DriverProfile.Create(OtherDriver.Id, Photo1, Photo2);
            App.DriverProfiles.Add(OtherProfile);
            OtherProfile.Approve(Vehicle.Create(OtherProfile.Id, 1, "51C-999.99", 1000, "200x150x150", Photo1, Photo1, Photo1));

            App.Settings.Add(PlatformSetting.Create(SettingKeys.CommissionRate, "0.10", TestApp.Start));
        }

        public ShippingOrder AcceptedOrder(
            PaymentMethod method = PaymentMethod.PayOS,
            PayerRole? payer = null,
            long? driverProfileId = null)
        {
            var order = ShippingOrder.Create(
                "MM20261108001", Customer.Id, vehicleTypeId: 1, couponId: null,
                "12 Nguyễn Huệ, Q1", 10.77m, 106.70m, "45 Lê Văn Việt, Thủ Đức", 10.84m, 106.78m,
                "Người Nhận", "0988777666", 12.5m, 35, hasLoadingService: true,
                distanceFee: 200_000m, serviceFee: 50_000m, discountAmount: 20_000m,
                method, method == PaymentMethod.Cash ? (payer ?? PayerRole.Sender) : null,
                CargoDetail.Create("FURNITURE", 2, 80, 120, 60, 60, [Photo1], "Hàng dễ vỡ"),
                TestApp.Start);
            if (method == PaymentMethod.PayOS)
                order.MarkPaid(TestApp.Start);

            order.Accept(driverProfileId ?? Profile.Id, Driver.Id, TestApp.Start);
            App.Orders.Add(order);
            return order;
        }
    }

    [Fact]
    public async Task Driver_uploads_jpg_into_driver_folder_and_rejects_spoofed_file_GB06()
    {
        var s = new Setup();

        var uploaded = await s.App.Sender.Send(
            new UploadDriverImageCommand(new MemoryStream(JpgBytes), "pickup.jpg", "image/jpeg", JpgBytes.Length));

        Assert.StartsWith("https://res.cloudinary.com/demo/", uploaded.Url);
        Assert.Equal($"modmove/drivers/{s.Driver.Id}", s.App.Files.Uploaded.Single().Folder);

        await Assert.ThrowsAsync<ValidationException>(() => s.App.Sender.Send(
            new UploadDriverImageCommand(new MemoryStream([0x4D, 0x5A, 0x90, 0x00]), "bad.jpg", "image/jpeg", 4)));
    }

    [Fact]
    public async Task An_onboarding_document_is_stored_private_in_its_own_folder_NFR_2_3()
    {
        var s = new Setup();

        var uploaded = await s.App.Sender.Send(new UploadDriverImageCommand(
            new MemoryStream(JpgBytes), "cccd.jpg", "image/jpeg", JpgBytes.Length, DriverUploadPurpose.Document));

        Assert.Contains("/image/authenticated/", uploaded.Url);
        Assert.Equal($"modmove/drivers/{s.Driver.Id}/documents", s.App.Files.UploadedPrivate.Single().Folder);
        Assert.Empty(s.App.Files.Uploaded);
        Assert.True(s.App.Files.IsOwnedUrl(uploaded.Url)); // still accepted by submit / resubmit
    }

    [Fact]
    public async Task ConfirmArrival_transitions_to_Arrived_and_notifies_customer_and_order_group_DRV12()
    {
        var s = new Setup();
        var order = s.AcceptedOrder();

        await s.App.Sender.Send(new ConfirmArrivalCommand(order.Id));

        Assert.Equal(OrderStatus.Arrived, order.OrderStatus);
        Assert.Equal(1, s.App.UnitOfWork.Saves);

        // B12: OrderStatusChanged sent to both order-{id} group and driver's user channel
        Assert.Contains(s.App.Realtime.Sent, m => m.Target == "order" && m.Id == order.Id.ToString() && m.EventName == RealtimeEvents.OrderStatusChanged);
        Assert.Contains(s.App.Realtime.Sent, m => m.Target == "user" && m.Id == s.Driver.Id.ToString() && m.EventName == RealtimeEvents.OrderStatusChanged);

        // 8.3: Customer notified via SignalR + FCM
        var customerNotif = Assert.Single(s.App.Realtime.Sent, m => m.Target == "user" && m.Id == s.Customer.Id.ToString() && m.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal("DRIVER_ARRIVED", Assert.IsType<Notification>(customerNotif.Payload).Type);
        Assert.Single(s.App.Push.Sent);
    }

    [Fact]
    public async Task ConfirmArrival_refuses_other_drivers_order_or_unapproved_driver()
    {
        var s = new Setup();
        var otherOrder = s.AcceptedOrder(driverProfileId: s.OtherProfile.Id);

        var notYours = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new ConfirmArrivalCommand(otherOrder.Id)));
        Assert.Equal("ORDER_NOT_YOURS", notYours.Code);

        var unapproved = new Setup(approved: false);
        var order2 = unapproved.AcceptedOrder();
        var notApproved = await Assert.ThrowsAsync<DomainException>(() => unapproved.App.Sender.Send(new ConfirmArrivalCommand(order2.Id)));
        Assert.Equal("DRIVER_NOT_APPROVED", notApproved.Code);
    }

    [Fact]
    public async Task ConfirmPickup_records_photos_allows_five_submissions_and_blocks_sixth_DRV13_6_3()
    {
        var s = new Setup();
        var order = s.AcceptedOrder();
        order.ConfirmArrival(s.Driver.Id, TestApp.Start);

        await s.App.Sender.Send(new ConfirmPickupCommand(order.Id, [Photo1, Photo2]));

        Assert.Equal(OrderStatus.PickedUp, order.OrderStatus);
        Assert.Equal((ProofStatus.Pending, 1), (order.Proof!.ProofStatus, order.Proof.PickupSubmissionCount));
        var notif = Assert.Single(s.App.Realtime.Sent, m => m.Target == "user" && m.Id == s.Customer.Id.ToString() && m.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal("PICKUP_PHOTOS_TO_REVIEW", Assert.IsType<Notification>(notif.Payload).Type);

        for (var attempt = 2; attempt <= 5; attempt++)
        {
            order.RejectPickupEvidence("Ảnh bị mờ", s.Customer.Id);
            await s.App.Sender.Send(new ConfirmPickupCommand(order.Id, [Photo1, Photo2]));
            Assert.Equal((ProofStatus.Pending, attempt), (order.Proof.ProofStatus, order.Proof.PickupSubmissionCount));
        }

        order.RejectPickupEvidence("Vẫn mờ", s.Customer.Id);
        var sixth = await Assert.ThrowsAsync<DomainException>(() =>
            s.App.Sender.Send(new ConfirmPickupCommand(order.Id, [Photo1, Photo2])));
        Assert.Equal("PICKUP_PHOTO_LIMIT", sixth.Code);
    }

    [Fact]
    public async Task Cash_order_delivery_supports_both_two_step_CollectCash_and_inline_CashCollected_DRV16_DRV17()
    {
        // Case 1: Receiver cash order without collecting cash first -> 422 CASH_NOT_COLLECTED; after arrive-dropoff, inline CashCollected = true with dropoff coordinates succeeds
        var s1 = new Setup();
        var receiverCashOrder = s1.AcceptedOrder(PaymentMethod.Cash, PayerRole.Receiver);
        receiverCashOrder.ConfirmArrival(s1.Driver.Id, TestApp.Start);
        receiverCashOrder.ConfirmPickup([Photo1, Photo2], s1.Driver.Id, TestApp.Start);
        receiverCashOrder.ApprovePickupEvidence(s1.Customer.Id, TestApp.Start);

        var missingCash = await Assert.ThrowsAsync<DomainException>(() =>
            s1.App.Sender.Send(new DeliverTripCommand(receiverCashOrder.Id, PodPhoto, CashCollected: false)));
        Assert.Equal("CASH_NOT_COLLECTED", missingCash.Code);
        Assert.Equal(OrderStatus.InTransit, receiverCashOrder.OrderStatus);

        // Must arrive at dropoff before collecting cash; the server does not check the distance (GB-52)
        await s1.App.Sender.Send(new NotifyArrivalAtDropoffCommand(receiverCashOrder.Id));
        Assert.NotNull(receiverCashOrder.ArrivedAtDropoffAt);

        // Far from the drop-off point: still accepted, without coordinates either.
        await s1.App.Sender.Send(new DeliverTripCommand(receiverCashOrder.Id, PodPhoto, CashCollected: true));

        Assert.Equal(OrderStatus.Delivered, receiverCashOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Paid, receiverCashOrder.PaymentStatus);
        var cashTx1 = Assert.Single(s1.App.Payments.Items);
        Assert.Equal((PaymentMethod.Cash, (PayerRole?)PayerRole.Receiver, 230_000m, PaymentTransactionStatus.Paid),
            (cashTx1.PaymentMethod, cashTx1.PayerRole, cashTx1.Amount, cashTx1.TransactionStatus));
        Assert.Contains(s1.App.Realtime.Sent, m =>
            m.Target == "user" && m.Id == s1.Customer.Id.ToString() &&
            m.EventName == RealtimeEvents.NotificationReceived &&
            ((Notification)m.Payload).Type == "ORDER_DELIVERED");

        // Case 2: Sender cash order collected at Arrived within 500 m via CollectCashCommand, duplicate rejected, then delivered with CashCollected = false
        var s2 = new Setup();
        var senderCashOrder = s2.AcceptedOrder(PaymentMethod.Cash, PayerRole.Sender);
        senderCashOrder.ConfirmArrival(s2.Driver.Id, TestApp.Start);

        await s2.App.Sender.Send(new CollectCashCommand(senderCashOrder.Id, senderCashOrder.PickupLatitude, senderCashOrder.PickupLongitude));
        Assert.Equal(PaymentStatus.Paid, senderCashOrder.PaymentStatus);
        Assert.Single(s2.App.Payments.Items);

        var duplicate = await Assert.ThrowsAsync<DomainException>(() =>
            s2.App.Sender.Send(new CollectCashCommand(senderCashOrder.Id, senderCashOrder.PickupLatitude, senderCashOrder.PickupLongitude)));
        Assert.Equal("ORDER_ALREADY_PAID", duplicate.Code);
        Assert.Single(s2.App.Payments.Items);

        senderCashOrder.ConfirmPickup([Photo1, Photo2], s2.Driver.Id, TestApp.Start);
        senderCashOrder.ApprovePickupEvidence(s2.Customer.Id, TestApp.Start);

        await s2.App.Sender.Send(new DeliverTripCommand(senderCashOrder.Id, PodPhoto));
        Assert.Equal(OrderStatus.Delivered, senderCashOrder.OrderStatus);
        Assert.Equal(PodPhoto, senderCashOrder.Proof!.DeliveryPhotoUrl);

        // Case 3: Zero-fare cash order (100% coupon discount) delivers cleanly and marks Paid
        var s3 = new Setup();
        var zeroCashOrder = ShippingOrder.Create(
            "MM20261108099", s3.Customer.Id, vehicleTypeId: 1, couponId: 1,
            "12 Nguyễn Huệ, Q1", 10.77m, 106.70m, "45 Lê Văn Việt, Thủ Đức", 10.84m, 106.78m,
            "Người Nhận", "0988777666", 5m, 15, hasLoadingService: false,
            distanceFee: 100_000m, serviceFee: 0m, discountAmount: 100_000m,
            PaymentMethod.Cash, PayerRole.Receiver,
            CargoDetail.Create("FURNITURE", 1, 20, 50, 50, 50, [Photo1], null),
            TestApp.Start);
        zeroCashOrder.Accept(s3.Profile.Id, s3.Driver.Id, TestApp.Start);
        s3.App.Orders.Add(zeroCashOrder);
        zeroCashOrder.ConfirmArrival(s3.Driver.Id, TestApp.Start);
        zeroCashOrder.ConfirmPickup([Photo1, Photo2], s3.Driver.Id, TestApp.Start);
        zeroCashOrder.ApprovePickupEvidence(s3.Customer.Id, TestApp.Start);

        await s3.App.Sender.Send(new DeliverTripCommand(zeroCashOrder.Id, PodPhoto));
        Assert.Equal(OrderStatus.Delivered, zeroCashOrder.OrderStatus);
        Assert.Equal(PaymentStatus.Paid, zeroCashOrder.PaymentStatus);
    }

    [Fact]
    public async Task NotifyArrivalAtDropoff_notifies_customer_without_changing_InTransit_status_DRV15()
    {
        var s = new Setup();
        var order = s.AcceptedOrder();

        // Cannot notify drop-off arrival before InTransit
        await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new NotifyArrivalAtDropoffCommand(order.Id)));

        order.ConfirmArrival(s.Driver.Id, TestApp.Start);
        order.ConfirmPickup([Photo1, Photo2], s.Driver.Id, TestApp.Start);
        order.ApprovePickupEvidence(s.Customer.Id, TestApp.Start);
        s.App.Realtime.Sent.Clear();
        s.App.Push.Sent.Clear();

        await s.App.Sender.Send(new NotifyArrivalAtDropoffCommand(order.Id));

        Assert.Equal(OrderStatus.InTransit, order.OrderStatus);
        Assert.DoesNotContain(s.App.Realtime.Sent, m => m.EventName == RealtimeEvents.OrderStatusChanged);
        var notif = Assert.Single(s.App.Realtime.Sent, m => m.Target == "user" && m.Id == s.Customer.Id.ToString() && m.EventName == RealtimeEvents.NotificationReceived);
        Assert.Equal("DRIVER_ARRIVED_DROPOFF", Assert.IsType<Notification>(notif.Payload).Type);
        Assert.Single(s.App.Push.Sent);

        // Other driver's order -> ORDER_NOT_YOURS
        var otherOrder = s.AcceptedOrder(driverProfileId: s.OtherProfile.Id);
        var notYours = await Assert.ThrowsAsync<DomainException>(() => s.App.Sender.Send(new NotifyArrivalAtDropoffCommand(otherOrder.Id)));
        Assert.Equal("ORDER_NOT_YOURS", notYours.Code);
    }

    [Fact]
    public async Task Driver_can_query_trip_list_and_trip_detail_with_handover_point_and_masked_customer_incident()
    {
        var s = new Setup();

        // 1. GetDriverTripsQuery
        s.App.Sql.Results.Enqueue(DriverContextTable(s.Profile.Id));
        s.App.Sql.ScalarResult = 1;
        var listTable = new DataTable();
        listTable.Columns.Add("Id", typeof(long));
        listTable.Columns.Add("OrderCode", typeof(string));
        listTable.Columns.Add("OrderStatus", typeof(string));
        listTable.Columns.Add("PickupAddress", typeof(string));
        listTable.Columns.Add("DropoffAddress", typeof(string));
        listTable.Columns.Add("VehicleTypeName", typeof(string));
        listTable.Columns.Add("EstimatedDistanceKm", typeof(decimal));
        listTable.Columns.Add("EstimatedDurationMin", typeof(int));
        listTable.Columns.Add("HasLoadingService", typeof(bool));
        listTable.Columns.Add("TotalFee", typeof(decimal));
        listTable.Columns.Add("CommissionAmount", typeof(decimal));
        listTable.Columns.Add("PaymentMethod", typeof(string));
        listTable.Columns.Add("PayerRole", typeof(string));
        listTable.Columns.Add("PaymentStatus", typeof(string));
        listTable.Columns.Add("CustomerName", typeof(string));
        listTable.Columns.Add("CustomerPhone", typeof(string));
        listTable.Columns.Add("ReceiverName", typeof(string));
        listTable.Columns.Add("ReceiverPhone", typeof(string));
        listTable.Columns.Add("CreatedAt", typeof(DateTime));
        listTable.Columns.Add("DeliveredAt", typeof(DateTime));
        listTable.Columns.Add("CompletedAt", typeof(DateTime));
        listTable.Rows.Add(
            55L, "MM20261108055", "Accepted", "Điểm lấy A", "Điểm giao B", "Xe tải 1 tấn",
            10.5m, 25, true, 200_000m, DBNull.Value, "Cash", "Receiver", "Unpaid",
            "Lê Thị Khách", "0909000001", "Người Nhận", "0988777666",
            TestApp.Start.UtcDateTime, DBNull.Value, DBNull.Value);
        s.App.Sql.Results.Enqueue(listTable);

        var list = await s.App.Sender.Send(new GetDriverTripsQuery(Page: 1, PageSize: 20, View: OrderView.Active));
        var summary = Assert.Single(list.Items);
        Assert.Equal((55L, 20_000m, 180_000m), (summary.Id, summary.CommissionAmount, summary.DriverEarning));

        // 2. GetActiveDriverTripQuery (reassigned order at Accepted with Proof -> returns HandoverPoint!)
        s.App.Sql.Results.Enqueue(DriverContextTable(s.Profile.Id));
        var activeIdTable = new DataTable();
        activeIdTable.Columns.Add("id", typeof(long));
        activeIdTable.Rows.Add(55L);
        s.App.Sql.Results.Enqueue(activeIdTable);

        var orderTable = new DataTable();
        orderTable.Columns.Add("Id", typeof(long));
        orderTable.Columns.Add("OrderCode", typeof(string));
        orderTable.Columns.Add("OrderStatus", typeof(string));
        orderTable.Columns.Add("PickupAddress", typeof(string));
        orderTable.Columns.Add("PickupLatitude", typeof(decimal));
        orderTable.Columns.Add("PickupLongitude", typeof(decimal));
        orderTable.Columns.Add("DropoffAddress", typeof(string));
        orderTable.Columns.Add("DropoffLatitude", typeof(decimal));
        orderTable.Columns.Add("DropoffLongitude", typeof(decimal));
        orderTable.Columns.Add("VehicleTypeName", typeof(string));
        orderTable.Columns.Add("EstimatedDistanceKm", typeof(decimal));
        orderTable.Columns.Add("EstimatedDurationMin", typeof(int));
        orderTable.Columns.Add("HasLoadingService", typeof(bool));
        orderTable.Columns.Add("CustomerName", typeof(string));
        orderTable.Columns.Add("CustomerPhone", typeof(string));
        orderTable.Columns.Add("ReceiverName", typeof(string));
        orderTable.Columns.Add("ReceiverPhone", typeof(string));
        orderTable.Columns.Add("DistanceFee", typeof(decimal));
        orderTable.Columns.Add("ServiceFee", typeof(decimal));
        orderTable.Columns.Add("DiscountAmount", typeof(decimal));
        orderTable.Columns.Add("TotalFee", typeof(decimal));
        orderTable.Columns.Add("CommissionRate", typeof(decimal));
        orderTable.Columns.Add("CommissionAmount", typeof(decimal));
        orderTable.Columns.Add("PaymentMethod", typeof(string));
        orderTable.Columns.Add("PayerRole", typeof(string));
        orderTable.Columns.Add("PaymentStatus", typeof(string));
        orderTable.Columns.Add("CreatedAt", typeof(DateTime));
        orderTable.Columns.Add("AcceptedAt", typeof(DateTime));
        orderTable.Columns.Add("PaidAt", typeof(DateTime));
        orderTable.Columns.Add("DeliveredAt", typeof(DateTime));
        orderTable.Columns.Add("CompletedAt", typeof(DateTime));
        orderTable.Columns.Add("CancellationReason", typeof(string));
        orderTable.Columns.Add("RatingStars", typeof(int));
        orderTable.Columns.Add("ReviewComment", typeof(string));
        orderTable.Columns.Add("RatedAt", typeof(DateTime));
        orderTable.Rows.Add(
            55L, "MM20261108055", "Accepted", "Điểm lấy A", 10.77m, 106.70m, "Điểm giao B", 10.84m, 106.78m,
            "Xe tải 1 tấn", 10.5m, 25, true, "Lê Thị Khách", "0909000001", "Người Nhận", "0988777666",
            180_000m, 40_000m, 20_000m, 200_000m, DBNull.Value, DBNull.Value, "Cash", "Receiver", "Unpaid",
            TestApp.Start.UtcDateTime, TestApp.Start.UtcDateTime, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value,
            DBNull.Value, DBNull.Value, DBNull.Value);
        s.App.Sql.Results.Enqueue(orderTable);

        var cargoTable = new DataTable();
        cargoTable.Columns.Add("CargoType", typeof(string));
        cargoTable.Columns.Add("CargoTypeName", typeof(string));
        cargoTable.Columns.Add("Quantity", typeof(int));
        cargoTable.Columns.Add("WeightKg", typeof(decimal));
        cargoTable.Columns.Add("LengthCm", typeof(int));
        cargoTable.Columns.Add("WidthCm", typeof(int));
        cargoTable.Columns.Add("HeightCm", typeof(int));
        cargoTable.Columns.Add("PhotoUrls", typeof(string));
        cargoTable.Columns.Add("SpecialNotes", typeof(string));
        cargoTable.Rows.Add("FURNITURE", "Đồ nội thất", 2, 80m, 120, 60, 60, "[\"https://res.cloudinary.com/demo/a.jpg\"]", null);
        s.App.Sql.Results.Enqueue(cargoTable);

        var proofTable = new DataTable();
        proofTable.Columns.Add("PickupPhotoUrls", typeof(string));
        proofTable.Columns.Add("ProofStatus", typeof(string));
        proofTable.Columns.Add("PickupSubmissionCount", typeof(int));
        proofTable.Columns.Add("RejectionReason", typeof(string));
        proofTable.Columns.Add("DeliveryPhotoUrl", typeof(string));
        proofTable.Columns.Add("DeliveryCompletionTime", typeof(DateTime));
        proofTable.Rows.Add("[\"https://res.cloudinary.com/demo/old1.jpg\"]", "Verified", 0, null, null, DBNull.Value);
        s.App.Sql.Results.Enqueue(proofTable);

        var historyTable = new DataTable();
        historyTable.Columns.Add("FromStatus", typeof(string));
        historyTable.Columns.Add("ToStatus", typeof(string));
        historyTable.Columns.Add("ChangedByRole", typeof(string));
        historyTable.Columns.Add("Note", typeof(string));
        historyTable.Columns.Add("ChangedAt", typeof(DateTime));
        historyTable.Rows.Add("InTransit", "Accepted", "Dispatcher", "Đổi tài xế do xe hỏng", TestApp.Start.UtcDateTime);
        s.App.Sql.Results.Enqueue(historyTable);

        var incidentsTable = new DataTable();
        incidentsTable.Columns.Add("Id", typeof(long));
        incidentsTable.Columns.Add("IncidentType", typeof(string));
        incidentsTable.Columns.Add("Description", typeof(string));
        incidentsTable.Columns.Add("PhotoUrls", typeof(string));
        incidentsTable.Columns.Add("ReportedBy", typeof(long));
        incidentsTable.Columns.Add("ResolutionStatus", typeof(string));
        incidentsTable.Columns.Add("ResolutionDecision", typeof(string));
        incidentsTable.Columns.Add("ResolutionNote", typeof(string));
        incidentsTable.Columns.Add("CreatedAt", typeof(DateTime));
        incidentsTable.Columns.Add("ResolvedAt", typeof(DateTime));
        // Customer-reported incident: its raw description must be masked for the driver!
        incidentsTable.Rows.Add(
            7L, "Other", "Tài xế chạy ẩu quá!", "[\"https://res.cloudinary.com/demo/secret.jpg\"]",
            s.Customer.Id, "Open", null, null, TestApp.Start.UtcDateTime, DBNull.Value);
        s.App.Sql.Results.Enqueue(incidentsTable);

        var handoverTable = new DataTable();
        handoverTable.Columns.Add("Latitude", typeof(decimal));
        handoverTable.Columns.Add("Longitude", typeof(decimal));
        handoverTable.Rows.Add(10.8012m, 106.7415m);
        s.App.Sql.Results.Enqueue(handoverTable);

        var detail = await s.App.Sender.Send(new GetActiveDriverTripQuery());
        Assert.NotNull(detail);
        Assert.Equal(new GeoPoint(10.8012m, 106.7415m), detail!.HandoverPoint);
        Assert.Equal(200_000m, detail.CashToCollect);
        Assert.Equal(5, detail.Proof!.RemainingPickupSubmissions);
        var inc = Assert.Single(detail.Incidents);
        Assert.False(inc.ReportedByYou);
        Assert.DoesNotContain("chạy ẩu", inc.Description);
        Assert.Empty(inc.PhotoUrls);

        // 3. GetDriverTripDetailQuery for unknown/unowned order -> NotFoundException
        s.App.Sql.Results.Enqueue(DriverContextTable(s.Profile.Id));
        s.App.Sql.Results.Enqueue(orderTable.Clone());
        await Assert.ThrowsAsync<NotFoundException>(() => s.App.Sender.Send(new GetDriverTripDetailQuery(999)));
    }

    private static DataTable DriverContextTable(long driverProfileId)
    {
        var dt = new DataTable();
        dt.Columns.Add("AccountStatus", typeof(string));
        dt.Columns.Add("DriverProfileId", typeof(long));
        dt.Columns.Add("ApprovalStatus", typeof(string));
        dt.Columns.Add("BankName", typeof(string));
        dt.Columns.Add("BankAccountNumber", typeof(string));
        dt.Columns.Add("BankAccountHolder", typeof(string));
        dt.Rows.Add("Active", driverProfileId, "Approved", "Vietcombank", "0071001234567", "NGUYEN VAN TAI");
        return dt;
    }
}

