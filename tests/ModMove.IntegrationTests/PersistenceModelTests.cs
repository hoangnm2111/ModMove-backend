using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Infrastructure.Persistence;
using ModMove.Infrastructure.Persistence.Migrations;
using ModMove.Infrastructure.Persistence.Repositories;

namespace ModMove.IntegrationTests;

/// <summary>Checks EF mapping rules that only show up under concurrent writes. Builds the model; no database needed.</summary>
public class PersistenceModelTests
{
    private static ApplicationDbContext NewContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql("Server=localhost;Database=none", new MySqlServerVersion(new Version(8, 0, 36)))
            .Options);

    [Theory]
    [InlineData(typeof(User), nameof(User.FailedLoginCount))]       // GB-02: parallel wrong passwords cannot reset the count
    [InlineData(typeof(ShippingOrder), nameof(ShippingOrder.RowVersion))] // GB-08: first driver to accept wins
    [InlineData(typeof(DriverWallet), nameof(DriverWallet.RowVersion))]   // balance changes never lost
    [InlineData(typeof(Coupon), nameof(Coupon.UsedCount))]                // B11, GB-21: two orders cannot share the last use
    [InlineData(typeof(Refund), nameof(Refund.RowVersion))]               // a withdrawal and an Admin decision cannot both win
    [InlineData(typeof(PaymentTransaction), nameof(PaymentTransaction.TransactionStatus))] // JOB-PAY-01 cannot overwrite Paid
    [InlineData(typeof(User), nameof(User.EmailVerified))]                // A-02: registering again cannot overwrite an account verified meanwhile
    [InlineData(typeof(OtpVerification), nameof(OtpVerification.CodeHash))]       // A-02, GB-25: a replaced code cannot verify, a resend cannot lose a count
    [InlineData(typeof(OtpVerification), nameof(OtpVerification.VerifiedAt))]     // a code is used once
    [InlineData(typeof(OtpVerification), nameof(OtpVerification.FailedAttempts))] // parallel guesses cannot overwrite the count: the loser gets 409
    [InlineData(typeof(InTransitIncident), nameof(InTransitIncident.ResolutionStatus))]   // Q7: two Dispatchers on one incident, the second gets 409
    [InlineData(typeof(DriverApplication), nameof(DriverApplication.ApplicationStatus))] // two Administrators approving and rejecting at once
    public void Column_is_a_concurrency_token(Type entity, string property)
    {
        using var db = NewContext();

        Assert.True(db.Model.FindEntityType(entity)!.FindProperty(property)!.IsConcurrencyToken);
    }

    [Fact]
    public void One_otp_row_per_email_and_purpose_so_two_first_sends_cannot_both_win_GB25()
    {
        using var db = NewContext();

        var index = Assert.Single(db.Model.FindEntityType(typeof(OtpVerification))!.GetIndexes(), i => i.IsUnique);
        Assert.Equal([nameof(OtpVerification.Email), nameof(OtpVerification.Purpose)], index.Properties.Select(p => p.Name));
    }

    [Fact]
    public void Commission_rate_is_locked_from_accepted_until_delivered_C8()
    {
        var inProgress = (OrderStatus[])typeof(ShippingOrderRepository)
            .GetField("InProgress", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.Equal([OrderStatus.Accepted, OrderStatus.Arrived, OrderStatus.PickedUp, OrderStatus.InTransit], inProgress);
    }

    [Fact]
    public void Rename_migration_updates_every_column_holding_an_order_status_both_ways()
    {
        using var db = NewContext();
        string Update(Type entity, string property, string from, string to)
        {
            var type = db.Model.FindEntityType(entity)!;
            var column = type.FindProperty(property)!.GetColumnName();
            return $"UPDATE `{type.GetTableName()}` SET `{column}` = '{to}' WHERE `{column}` = '{from}';";
        }
        string[] Expected(string from, string to) =>
        [
            Update(typeof(ShippingOrder), nameof(ShippingOrder.OrderStatus), from, to),
            Update(typeof(OrderStatusHistory), nameof(OrderStatusHistory.FromStatus), from, to),
            Update(typeof(OrderStatusHistory), nameof(OrderStatusHistory.ToStatus), from, to),
        ];
        var migration = new RenameOrderStatusPendingToPlaceOrder();
        var back = new RenameOrderStatusPlaceOrderBackToPending();

        Assert.Equal(Expected("Pending", "PlaceOrder"), migration.UpOperations.Cast<SqlOperation>().Select(o => o.Sql));
        Assert.Equal(Expected("PlaceOrder", "Pending"), migration.DownOperations.Cast<SqlOperation>().Select(o => o.Sql));
        Assert.Equal(Expected("PlaceOrder", "Pending"), back.UpOperations.Cast<SqlOperation>().Select(o => o.Sql));
        Assert.Equal(Expected("Pending", "PlaceOrder"), back.DownOperations.Cast<SqlOperation>().Select(o => o.Sql));
    }

    [Fact]
    public void Every_wallet_transaction_type_fits_its_column()
    {
        using var db = NewContext();
        var maxLength = db.Model.FindEntityType(typeof(WalletTransaction))!.FindProperty(nameof(WalletTransaction.TransactionType))!.GetMaxLength();

        Assert.All(Enum.GetNames<WalletTransactionType>(), name => Assert.True(name.Length <= maxLength, name));
    }
}
