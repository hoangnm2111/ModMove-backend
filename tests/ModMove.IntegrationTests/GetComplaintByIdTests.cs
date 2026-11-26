using System.Data;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Refunds.Queries.GetComplaintById;

namespace ModMove.IntegrationTests;

/// <summary>GET api/dispatcher/complaints/{id} (UC-DIS-12, 13) through the real pipeline with <see cref="FakeSql"/>.</summary>
public class GetComplaintByIdTests
{
    private static ISender SenderWith(FakeSql sql) => new ServiceCollection()
        .AddLogging()
        .AddApplication()
        .AddSingleton<ISqlConnectionFactory>(sql)
        .AddSingleton<IFileStorage>(new FakeFileStorage())
        .BuildServiceProvider()
        .GetRequiredService<ISender>();

    private static DataTable CreateComplaintTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("OrderId", typeof(long));
        table.Columns.Add("OrderCode", typeof(string));
        table.Columns.Add("OrderStatus", typeof(string));
        table.Columns.Add("ReasonCode", typeof(string));
        table.Columns.Add("ReasonCodeName", typeof(string));
        table.Columns.Add("Reason", typeof(string));
        table.Columns.Add("EvidenceUrlsJson", typeof(string));
        table.Columns.Add("RequestedAmount", typeof(decimal));
        table.Columns.Add("ProposedAmount", typeof(decimal));
        table.Columns.Add("ProposalReason", typeof(string));
        table.Columns.Add("ProposedById", typeof(long));
        table.Columns.Add("ProposedByName", typeof(string));
        table.Columns.Add("InfoRequestNote", typeof(string));
        table.Columns.Add("CustomerReply", typeof(string));
        table.Columns.Add("RefundStatus", typeof(string));
        table.Columns.Add("BankReferenceCode", typeof(string));
        table.Columns.Add("DecidedAt", typeof(DateTime));
        table.Columns.Add("ProcessedAt", typeof(DateTime));
        table.Columns.Add("CreatedAt", typeof(DateTime));

        // Order
        table.Columns.Add("PickupAddress", typeof(string));
        table.Columns.Add("DropoffAddress", typeof(string));
        table.Columns.Add("PaymentMethod", typeof(string));
        table.Columns.Add("PaymentStatus", typeof(string));
        table.Columns.Add("TotalFee", typeof(decimal));
        table.Columns.Add("DeliveredAt", typeof(DateTime));
        table.Columns.Add("CompletedAt", typeof(DateTime));

        // Cargo
        table.Columns.Add("CargoType", typeof(string));
        table.Columns.Add("CargoTypeName", typeof(string));
        table.Columns.Add("CargoQuantity", typeof(int));
        table.Columns.Add("CargoWeightKg", typeof(decimal));
        table.Columns.Add("CargoLengthCm", typeof(int));
        table.Columns.Add("CargoWidthCm", typeof(int));
        table.Columns.Add("CargoHeightCm", typeof(int));
        table.Columns.Add("CargoSpecialNotes", typeof(string));
        table.Columns.Add("CargoPhotoUrlsJson", typeof(string));

        // Customer
        table.Columns.Add("CustomerId", typeof(long));
        table.Columns.Add("CustomerName", typeof(string));
        table.Columns.Add("CustomerPhone", typeof(string));

        // Driver
        table.Columns.Add("DriverProfileId", typeof(long));
        table.Columns.Add("DriverUserId", typeof(long));
        table.Columns.Add("DriverFullName", typeof(string));
        table.Columns.Add("DriverPhone", typeof(string));
        table.Columns.Add("LicensePlate", typeof(string));
        table.Columns.Add("VehicleTypeName", typeof(string));

        // POD evidence
        table.Columns.Add("PickupPhotoUrlsJson", typeof(string));
        table.Columns.Add("DeliveryPhotoUrl", typeof(string));
        table.Columns.Add("DeliveryCompletionTime", typeof(DateTime));
        table.Columns.Add("ProofStatus", typeof(string));

        // Rating
        table.Columns.Add("RatingStars", typeof(int));
        table.Columns.Add("ReviewComment", typeof(string));
        table.Columns.Add("RatingCreatedAt", typeof(DateTime));

        return table;
    }

    private static DataTable CreateHistoriesTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("FromStatus", typeof(string));
        table.Columns.Add("ToStatus", typeof(string));
        table.Columns.Add("ChangedByRole", typeof(string));
        table.Columns.Add("ChangedByUserId", typeof(long));
        table.Columns.Add("ChangedByUserName", typeof(string));
        table.Columns.Add("Note", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        return table;
    }

    private static DataTable CreateIncidentsTable()
    {
        var table = new DataTable();
        table.Columns.Add("Id", typeof(long));
        table.Columns.Add("IncidentType", typeof(string));
        table.Columns.Add("Description", typeof(string));
        table.Columns.Add("ResolutionStatus", typeof(string));
        table.Columns.Add("ResolutionDecision", typeof(string));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        return table;
    }

    [Fact]
    public async Task When_complaint_not_found_throws_NotFoundException()
    {
        var sql = new FakeSql { Result = CreateComplaintTable() };

        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => SenderWith(sql).Send(new GetComplaintByIdQuery(999)));

        Assert.Contains("Refund", ex.Message);
    }

    [Fact]
    public async Task GetComplaintById_signs_private_photos_DEF011()
    {
        const string json = FakeFileStorage.MixedPhotosJson;
        var sql = new FakeSql
        {
            CustomQuery = (text, _) => text.Contains("FROM refunds")
                ? FakeSql.Row(("Id", 1L), ("EvidenceUrlsJson", json), ("CargoType", "FURNITURE"), ("CargoPhotoUrlsJson", json),
                    ("ProofStatus", "Verified"), ("PickupPhotoUrlsJson", json), ("DeliveryPhotoUrl", FakeFileStorage.PrivatePhoto))
                : new DataTable(),
        };

        var detail = await SenderWith(sql).Send(new GetComplaintByIdQuery(1));

        Assert.Equal(FakeFileStorage.MixedPhotosViewed, detail.EvidenceUrls);
        Assert.Equal(FakeFileStorage.MixedPhotosViewed, detail.Order.Cargo!.PhotoUrls);
        Assert.Equal(FakeFileStorage.MixedPhotosViewed, detail.VerificationEvidence!.PickupPhotoUrls);
        Assert.Equal(FakeFileStorage.SignedPhoto, detail.VerificationEvidence.DeliveryPhotoUrl);
    }

    [Fact]
    public async Task Maps_full_complaint_details_and_verification_evidence()
    {
        var sql = new FakeSql();

        var complaintTable = CreateComplaintTable();
        var now = new DateTime(2026, 10, 20, 10, 0, 0, DateTimeKind.Unspecified);
        complaintTable.Rows.Add(
            1L, 10L, "MMDEMO006", "Delivered", "DAMAGED_CARGO", "Hàng bị hư hỏng", "Hàng bị trầy xước", "[\"c_img1\"]",
            240000m, 120000m, "Hư hỏng 50%", 2L, "Dispatcher ModMove",
            "Cần thêm ảnh mặt đáy", "Đã gửi thêm ảnh", "Requested", DBNull.Value, DBNull.Value, DBNull.Value, now,
            "12 Nguyễn Huệ, Q1", "45 Lê Văn Việt, Thủ Đức", "PayOS", "Paid", 240000m, now.AddHours(-1), DBNull.Value,
            "FURNITURE", "Đồ nội thất", 1, 40m, 120, 60, 80, "Cẩn thận", "[\"cargo1\",\"cargo2\"]",
            3L, "Khách hàng Demo", "0900000003",
            1L, 4L, "Tài xế Demo", "0900000004", "51C-123.45", "Xe tải 1 tấn",
            "[\"p_img1\",\"p_img2\"]", "d_img1", now.AddHours(-1), "Verified",
            2, "Hàng bị xước", now.AddMinutes(-30));
        sql.Results.Enqueue(complaintTable);

        var historyTable = CreateHistoriesTable();
        historyTable.Rows.Add(101L, DBNull.Value, "Pending", "Customer", 3L, "Khách hàng Demo", null, now.AddHours(-4));
        sql.Results.Enqueue(historyTable);

        var incidentTable = CreateIncidentsTable();
        incidentTable.Rows.Add(51L, "DamagedCargo", "Khách thấy xước", "InProgress", DBNull.Value, now.AddHours(-2));
        sql.Results.Enqueue(incidentTable);

        var res = await SenderWith(sql).Send(new GetComplaintByIdQuery(1));

        Assert.Equal(1, res.Id);
        Assert.Equal(10, res.OrderId);
        Assert.Equal("MMDEMO006", res.OrderCode);
        Assert.Equal("DAMAGED_CARGO", res.ReasonCode);
        Assert.Equal("Hàng bị hư hỏng", res.ReasonCodeName);
        Assert.Equal("Hàng bị trầy xước", res.Reason);
        Assert.Equal(["c_img1"], res.EvidenceUrls);
        Assert.Equal(240000m, res.RequestedAmount);
        Assert.Equal(120000m, res.ProposedAmount);
        Assert.Equal("Hư hỏng 50%", res.ProposalReason);
        Assert.Equal("ProposedToAdmin", res.Stage);
        Assert.Equal(2, res.ProposedById);
        Assert.Equal("Dispatcher ModMove", res.ProposedByName);

        // Cargo as the customer declared it, to compare with the pickup and delivery photos (UC-DIS-13)
        Assert.NotNull(res.Order.Cargo);
        Assert.Equal("FURNITURE", res.Order.Cargo!.CargoType);
        Assert.Equal("Đồ nội thất", res.Order.Cargo.CargoTypeName);
        Assert.Equal(["cargo1", "cargo2"], res.Order.Cargo.PhotoUrls);

        // Verification evidence
        Assert.NotNull(res.VerificationEvidence);
        Assert.Equal(["p_img1", "p_img2"], res.VerificationEvidence!.PickupPhotoUrls);
        Assert.Equal("d_img1", res.VerificationEvidence.DeliveryPhotoUrl);
        Assert.Equal("Verified", res.VerificationEvidence.ProofStatus);

        // Rating
        Assert.NotNull(res.Rating);
        Assert.Equal(2, res.Rating!.RatingStars);
        Assert.Equal("Hàng bị xước", res.Rating.ReviewComment);

        // Incidents
        Assert.Single(res.Incidents);
        Assert.Equal(51, res.Incidents[0].Id);
        Assert.Equal("DamagedCargo", res.Incidents[0].IncidentType);

        // Status histories
        Assert.Single(res.StatusHistories);
        Assert.Equal("Pending", res.StatusHistories[0].ToStatus);
    }
}
