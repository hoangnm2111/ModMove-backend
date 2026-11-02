using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;
using ModMove.Infrastructure.Persistence;
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
    public void Column_is_a_concurrency_token(Type entity, string property)
    {
        using var db = NewContext();

        Assert.True(db.Model.FindEntityType(entity)!.FindProperty(property)!.IsConcurrencyToken);
    }

    [Fact]
    public void Commission_rate_is_locked_from_accepted_until_delivered_C8()
    {
        var inProgress = (OrderStatus[])typeof(ShippingOrderRepository)
            .GetField("InProgress", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.Equal([OrderStatus.Accepted, OrderStatus.Arrived, OrderStatus.PickedUp, OrderStatus.InTransit], inProgress);
    }

    [Fact]
    public void Every_wallet_transaction_type_fits_its_column()
    {
        using var db = NewContext();
        var maxLength = db.Model.FindEntityType(typeof(WalletTransaction))!.FindProperty(nameof(WalletTransaction.TransactionType))!.GetMaxLength();

        Assert.All(Enum.GetNames<WalletTransactionType>(), name => Assert.True(name.Length <= maxLength, name));
    }
}
