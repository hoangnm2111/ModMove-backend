using Microsoft.EntityFrameworkCore;
using ModMove.Domain.Entities;
using ModMove.Infrastructure.Persistence;

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
    public void Column_is_a_concurrency_token(Type entity, string property)
    {
        using var db = NewContext();

        Assert.True(db.Model.FindEntityType(entity)!.FindProperty(property)!.IsConcurrencyToken);
    }
}
