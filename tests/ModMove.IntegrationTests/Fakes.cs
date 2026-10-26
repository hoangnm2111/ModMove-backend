using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

// In-memory stand-ins for the ports, so handler tests run the real MediatR pipeline without a database (DECISIONS C2).

/// <summary>Gives entities ids the way the database would (1, 2, 3...), so tests can tell users apart.</summary>
internal static class TestIds
{
    private static readonly System.Reflection.PropertyInfo IdProperty = typeof(ModMove.Domain.Common.BaseEntity).GetProperty("Id")!;

    public static void Set(ModMove.Domain.Common.BaseEntity entity, long id) => IdProperty.SetValue(entity, id);
}

internal sealed class InMemoryUsers : IUserRepository
{
    public InMemoryUsers(params User[] seed)
    {
        foreach (var user in seed)
            Add(user);
    }

    public List<User> Items { get; } = [];
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

    public Task<bool> IsEmailOrPhoneTakenAsync(string email, string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(u => u.Email == email || u.PhoneNumber == phoneNumber));

    public Task<bool> IsPhoneTakenByAnotherAsync(string phoneNumber, long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(u => u.PhoneNumber == phoneNumber && u.Id != userId));

    public Task ReleaseFcmTokenAsync(string token, long keepForUserId, CancellationToken cancellationToken = default)
    {
        foreach (var user in Items.Where(u => u.FcmToken == token && u.Id != keepForUserId))
            user.SetFcmToken(null);
        return Task.CompletedTask;
    }

    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public void Add(User aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(User aggregate) { }
    public void Remove(User aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryOtps : IOtpRepository
{
    public List<OtpVerification> Items { get; } = [];

    public Task<OtpVerification?> GetLatestAsync(string email, OtpPurpose purpose, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.LastOrDefault(o => o.Email == email && o.Purpose == purpose));

    public Task<OtpVerification?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.Id == id));

    public void Add(OtpVerification aggregate) => Items.Add(aggregate);
    public void Update(OtpVerification aggregate) { }
    public void Remove(OtpVerification aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryOrders : IShippingOrderRepository
{
    /// <summary>Customers that still have an order neither Completed nor Cancelled.</summary>
    public HashSet<long> CustomersWithOpenOrders { get; } = [];

    /// <summary>Set to true to pretend an order is between Accepted and Delivered (DECISIONS C8).</summary>
    public bool HasOrdersInProgress { get; set; }

    public Task<bool> HasOpenOrdersForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomersWithOpenOrders.Contains(customerId));

    public Task<bool> HasOrdersInProgressAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(HasOrdersInProgress);

    public Task<ShippingOrder?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult<ShippingOrder?>(null);
    public void Add(ShippingOrder aggregate) { }
    public void Update(ShippingOrder aggregate) { }
    public void Remove(ShippingOrder aggregate) { }
}

internal sealed class InMemoryRepository<T> : IRepository<T> where T : ModMove.Domain.Common.BaseEntity, ModMove.Domain.Common.IAggregateRoot
{
    public List<T> Items { get; } = [];

    public Task<T?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public void Add(T aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(T aggregate) { }
    public void Remove(T aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryCoupons : ICouponRepository
{
    public List<Coupon> Items { get; } = [];

    public Task<Coupon?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.DiscountCode.Equals(code, StringComparison.OrdinalIgnoreCase)));

    public Task<Coupon?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(c => c.Id == id));

    public void Add(Coupon aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(Coupon aggregate) { }
    public void Remove(Coupon aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryDriverProfiles : IDriverProfileRepository
{
    public List<DriverProfile> Items { get; } = [];

    public Task<DriverProfile?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

    public Task<DriverProfile?> GetByIdWithApplicationsAndVehicleAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

    public Task<DriverProfile?> GetByApplicationIdWithApplicationsAndVehicleAsync(long applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Applications.Any(a => a.Id == applicationId)));

    public void Add(DriverProfile aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(DriverProfile aggregate) { }
    public void Remove(DriverProfile aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryDriverWallets : IDriverWalletRepository
{
    public List<DriverWallet> Items { get; } = [];

    public Task<DriverWallet?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(w => w.Id == id));

    public Task<DriverWallet?> GetByDriverProfileIdAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(w => w.DriverProfileId == driverProfileId));

    public void Add(DriverWallet aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(DriverWallet aggregate) { }
    public void Remove(DriverWallet aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryRefreshTokens : IRefreshTokenRepository
{
    public List<RefreshToken> Items { get; } = [];

    public Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task RevokeAllForUserAsync(long userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        foreach (var token in Items.Where(t => t.UserId == userId))
            token.Revoke(now);
        return Task.CompletedTask;
    }

    public Task<RefreshToken?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id));
    public void Add(RefreshToken aggregate) => Items.Add(aggregate);
    public void Update(RefreshToken aggregate) { }
    public void Remove(RefreshToken aggregate) => Items.Remove(aggregate);
}

internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int Saves { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "h:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "h:" + password;
}

internal sealed class FakeTokens : IJwtTokenService
{
    public string CreateAccessToken(long userId, UserRole role) => $"token-{role}";
}

internal sealed class MutableTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class CapturingEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        Sent.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }

    /// <summary>The 6-digit code in the last email (the OTP emails show it in bold).</summary>
    public string LastCode() => Regex.Match(Sent[^1].Body, "<b>([0-9]{6})</b>").Groups[1].Value;
}

internal sealed class CapturingPushNotifications : IPushNotificationService
{
    public List<(string Token, string Title, string Body, IReadOnlyDictionary<string, string>? Data)> Sent { get; } = [];

    public Task SendAsync(
        string deviceToken, string title, string body,
        IReadOnlyDictionary<string, string>? data, CancellationToken cancellationToken)
    {
        Sent.Add((deviceToken, title, body, data));
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(long? userId) : ICurrentUserService
{
    public long? UserId { get; } = userId;

    public bool IsInRole(string role) => false;
}

/// <summary>One wired-up Application with fakes; tests reach into the fakes to check what happened.</summary>
internal sealed class TestApp
{
    public static readonly DateTimeOffset Start = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    public InMemoryUsers Users { get; }
    public InMemoryOtps Otps { get; } = new();
    public InMemoryOrders Orders { get; } = new();
    public InMemoryRefreshTokens RefreshTokens { get; } = new();
    public InMemoryRepository<SupportRequest> SupportRequests { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public CapturingEmailSender Email { get; } = new();
    public MutableTime Time { get; } = new(Start);
    public ISender Sender { get; }

    /// <summary>App where <paramref name="user"/> is the signed-in caller; ids are assigned first, like a saved row.</summary>
    public static TestApp SignedInAs(User user, params User[] others)
    {
        var users = new InMemoryUsers([user, .. others]);
        return new TestApp(user.Id, users);
    }

    public TestApp(long? signedInUserId = null, params User[] users) : this(signedInUserId, new InMemoryUsers(users)) { }

    private TestApp(long? signedInUserId, InMemoryUsers users)
    {
        Users = users;
        Sender = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<IUserRepository>(Users)
            .AddSingleton<IOtpRepository>(Otps)
            .AddSingleton<IShippingOrderRepository>(Orders)
            .AddSingleton<IRefreshTokenRepository>(RefreshTokens)
            .AddSingleton<IRepository<SupportRequest>>(SupportRequests)
            .AddSingleton<IUnitOfWork>(UnitOfWork)
            .AddSingleton<IPasswordHasher>(new FakeHasher())
            .AddSingleton<IJwtTokenService>(new FakeTokens())
            .AddSingleton<IEmailSender>(Email)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedInUserId))
            .AddSingleton<TimeProvider>(Time)
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }
}
