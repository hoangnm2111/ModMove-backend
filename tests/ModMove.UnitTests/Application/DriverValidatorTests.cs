using FluentValidation.TestHelper;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Features.Incidents.Commands.ReportIncidentByDriver;
using ModMove.Application.Features.Incidents.Queries.GetDriverIncidents;
using ModMove.Application.Features.Matching.Queries.GetNearbyOrderDetail;
using ModMove.Application.Features.Orders.Queries.GetMyOrders;
using ModMove.Application.Features.Ratings.Queries.GetDriverRatings;
using ModMove.Application.Features.Trips.Commands.ConfirmPickup;
using ModMove.Application.Features.Trips.Commands.DeliverTrip;
using ModMove.Application.Features.Trips.Commands.NotifyArrivalAtDropoff;
using ModMove.Application.Features.Trips.Queries.GetDriverTrips;
using ModMove.Application.Features.Wallets.Commands.RequestWithdrawal;
using ModMove.Application.Features.Wallets.Commands.UpdateDriverBankAccount;
using ModMove.Application.Features.Wallets.Queries.GetDriverEarnings;
using ModMove.Application.Features.Wallets.Queries.GetDriverWalletTransactions;
using ModMove.Application.Features.Wallets.Queries.GetDriverWithdrawals;
using ModMove.Domain.Enums;

namespace ModMove.UnitTests.Application;

public class DriverValidatorTests
{
    private const string OwnedUrl1 = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/1.jpg";
    private const string OwnedUrl2 = "https://res.cloudinary.com/demo/image/upload/modmove/drivers/1/2.jpg";

    private sealed class StubStorage : IFileStorage
    {
        public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken) =>
            Task.FromResult(OwnedUrl1);

        public bool IsOwnedUrl(string url) =>
            url.StartsWith("https://res.cloudinary.com/demo/", StringComparison.Ordinal);
    }

    [Fact]
    public void ConfirmPickup_validator_accepts_2_to_5_owned_urls_and_rejects_invalid_inputs()
    {
        var validator = new ConfirmPickupCommandValidator(new StubStorage());

        validator.TestValidate(new ConfirmPickupCommand(1, [OwnedUrl1, OwnedUrl2]))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new ConfirmPickupCommand(0, [OwnedUrl1, OwnedUrl2]))
            .ShouldHaveValidationErrorFor(x => x.OrderId);

        validator.TestValidate(new ConfirmPickupCommand(1, [OwnedUrl1]))
            .ShouldHaveValidationErrorFor(x => x.PhotoUrls);

        validator.TestValidate(new ConfirmPickupCommand(1, [OwnedUrl1, OwnedUrl2, OwnedUrl1, OwnedUrl2, OwnedUrl1, OwnedUrl2]))
            .ShouldHaveValidationErrorFor(x => x.PhotoUrls);

        validator.TestValidate(new ConfirmPickupCommand(1, [OwnedUrl1, "https://evil.example.com/x.jpg"]))
            .ShouldHaveValidationErrorFor(x => x.PhotoUrls);
    }

    [Fact]
    public void DeliverTrip_validator_requires_owned_pod_url()
    {
        var validator = new DeliverTripCommandValidator(new StubStorage());

        validator.TestValidate(new DeliverTripCommand(1, OwnedUrl1, CashCollected: true))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new DeliverTripCommand(1, "https://other.example.com/pod.jpg"))
            .ShouldHaveValidationErrorFor(x => x.DeliveryPhotoUrl);
    }

    [Fact]
    public void ReportIncidentByDriver_validator_checks_description_photos_and_coordinate_pair()
    {
        var validator = new ReportIncidentByDriverCommandValidator(new StubStorage());

        validator.TestValidate(new ReportIncidentByDriverCommand(
                1, IncidentType.VehicleBreakdown, "Thủng lốp trên cầu Sài Gòn", [OwnedUrl1], 10.799m, 106.722m))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new ReportIncidentByDriverCommand(
                1, IncidentType.Other, "Khách vắng mặt tại điểm lấy"))
            .ShouldNotHaveAnyValidationErrors();

        validator.TestValidate(new ReportIncidentByDriverCommand(
                1, IncidentType.Other, "Chỉ có vĩ độ", null, Latitude: 10.7m, Longitude: null))
            .ShouldHaveAnyValidationError();

        validator.TestValidate(new ReportIncidentByDriverCommand(
                1, IncidentType.Accident, "", null))
            .ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void UpdateDriverBankAccount_and_RequestWithdrawal_validators_enforce_rules()
    {
        var bankValidator = new UpdateDriverBankAccountCommandValidator();
        bankValidator.TestValidate(new UpdateDriverBankAccountCommand("Vietcombank", "0071001234567", "NGUYEN VAN TAI"))
            .ShouldNotHaveAnyValidationErrors();
        bankValidator.TestValidate(new UpdateDriverBankAccountCommand("Vietcombank", "123", "NGUYEN VAN TAI"))
            .ShouldHaveValidationErrorFor(x => x.BankAccountNumber);

        var withdrawalValidator = new RequestWithdrawalCommandValidator();
        withdrawalValidator.TestValidate(new RequestWithdrawalCommand(1_000m))
            .ShouldNotHaveAnyValidationErrors();
        withdrawalValidator.TestValidate(new RequestWithdrawalCommand(100_000m, "ACB", "12345678", "NGUYEN VAN TAI"))
            .ShouldNotHaveAnyValidationErrors();
        withdrawalValidator.TestValidate(new RequestWithdrawalCommand(0m))
            .ShouldHaveValidationErrorFor(x => x.Amount);
        withdrawalValidator.TestValidate(new RequestWithdrawalCommand(1000.5m))
            .ShouldHaveValidationErrorFor(x => x.Amount);
        withdrawalValidator.TestValidate(new RequestWithdrawalCommand(50_000m, BankName: "ACB", BankAccountNumber: null, BankAccountHolder: null))
            .ShouldHaveValidationErrorFor(x => x.BankAccountNumber);
    }

    [Fact]
    public void Driver_query_validators_enforce_pagination_and_date_ranges()
    {
        var tripsValidator = new GetDriverTripsQueryValidator();
        tripsValidator.TestValidate(new GetDriverTripsQuery(1, 20, OrderView.Active)).ShouldNotHaveAnyValidationErrors();
        tripsValidator.TestValidate(new GetDriverTripsQuery(FromDate: new DateOnly(2026, 11, 10), ToDate: new DateOnly(2026, 11, 1)))
            .ShouldHaveValidationErrorFor(x => x.ToDate);

        var incidentsValidator = new GetDriverIncidentsQueryValidator();
        incidentsValidator.TestValidate(new GetDriverIncidentsQuery(1, 20, OrderId: 5, Status: IncidentResolutionStatus.Open))
            .ShouldNotHaveAnyValidationErrors();
        incidentsValidator.TestValidate(new GetDriverIncidentsQuery(OrderId: 0))
            .ShouldHaveValidationErrorFor(x => x.OrderId);

        var txValidator = new GetDriverWalletTransactionsQueryValidator();
        txValidator.TestValidate(new GetDriverWalletTransactionsQuery(1, 20, WalletTransactionType.TripEarning, WalletTransactionStatus.Completed))
            .ShouldNotHaveAnyValidationErrors();

        var withdrawalsValidator = new GetDriverWithdrawalsQueryValidator();
        withdrawalsValidator.TestValidate(new GetDriverWithdrawalsQuery(1, 51))
            .ShouldHaveValidationErrorFor(x => x.PageSize);

        var earningsValidator = new GetDriverEarningsQueryValidator();
        earningsValidator.TestValidate(new GetDriverEarningsQuery(EarningsPeriod.Month)).ShouldNotHaveAnyValidationErrors();
        earningsValidator.TestValidate(new GetDriverEarningsQuery(FromDate: new DateOnly(2026, 11, 8), ToDate: new DateOnly(2026, 11, 7)))
            .ShouldHaveValidationErrorFor(x => x.ToDate);

        var orderDetailValidator = new GetNearbyOrderDetailQueryValidator();
        orderDetailValidator.TestValidate(new GetNearbyOrderDetailQuery(1)).ShouldNotHaveAnyValidationErrors();
        orderDetailValidator.TestValidate(new GetNearbyOrderDetailQuery(0)).ShouldHaveValidationErrorFor(x => x.OrderId);

        var arriveDropoffValidator = new NotifyArrivalAtDropoffCommandValidator();
        arriveDropoffValidator.TestValidate(new NotifyArrivalAtDropoffCommand(1)).ShouldNotHaveAnyValidationErrors();
        arriveDropoffValidator.TestValidate(new NotifyArrivalAtDropoffCommand(0)).ShouldHaveValidationErrorFor(x => x.OrderId);

        var ratingsValidator = new GetDriverRatingsQueryValidator();
        ratingsValidator.TestValidate(new GetDriverRatingsQuery(1, 20, Stars: 5)).ShouldNotHaveAnyValidationErrors();
        ratingsValidator.TestValidate(new GetDriverRatingsQuery(1, 20, Stars: 6)).ShouldHaveValidationErrorFor(x => x.Stars);
    }
}

