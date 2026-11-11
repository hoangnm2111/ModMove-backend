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

    /// <summary>Orders added by commands; CreatedAt stays unset unless a test stamps it like the database would.</summary>
    public List<ShippingOrder> Items { get; } = [];

    public Task<int> CountCreatedBetweenAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Count(o => o.CreatedAt >= fromUtc && o.CreatedAt < toUtc));

    public Task<IReadOnlyList<ShippingOrder>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShippingOrder>>(Items.Where(o => o.OrderStatus == OrderStatus.Pending).ToList());

    public Task<IReadOnlyList<ShippingOrder>> GetDeliveredOrdersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShippingOrder>>(Items.Where(o => o.OrderStatus == OrderStatus.Delivered && o.DeliveredAt is not null).ToList());

    public Task<ShippingOrder?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.Id == id));

    public Task<ShippingOrder?> GetWithProofAsync(long id, CancellationToken cancellationToken = default) =>
        GetByIdAsync(id, cancellationToken);

    public Task<long?> GetActiveOrderIdForDriverAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(o => o.DriverProfileId == driverProfileId
            && o.OrderStatus is OrderStatus.Accepted or OrderStatus.Arrived or OrderStatus.PickedUp or OrderStatus.InTransit)?.Id);

    public void Add(ShippingOrder aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
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

/// <summary>The list entries the customer commands choose from, as ReferenceDataSeeder loads them.</summary>
internal sealed class InMemoryMasterData : IMasterDataRepository
{
    public List<MasterDataItem> Items { get; } =
    [
        MasterDataItem.Create("CARGO_TYPE", "FURNITURE", "Đồ nội thất"),
        MasterDataItem.Create("COMPLAINT_REASON", "DAMAGED_CARGO", "Hàng bị hư hỏng"),
        MasterDataItem.Create("COMPLAINT_REASON", "OTHER", "Khác"),
        MasterDataItem.Create("CANCELLATION_REASON", "CHANGED_PLAN", "Thay đổi kế hoạch"),
        MasterDataItem.Create("CANCELLATION_REASON", "OTHER", "Khác"),
        MasterDataItem.Create("BANK", "970436", "Vietcombank"), // R-01: code = BIN
        MasterDataItem.Create("BANK", "970415", "VietinBank"),
    ];

    public Task<MasterDataItem?> GetActiveAsync(string category, string code, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Category == category && SameAsMySql(i.Code, code) && i.IsActive));

    // utf8mb4_unicode_ci ignores case and accents.
    private static bool SameAsMySql(string left, string right) =>
        System.Globalization.CultureInfo.InvariantCulture.CompareInfo.Compare(
            left, right, System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) == 0;

    public Task<MasterDataItem?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public void Add(MasterDataItem aggregate) => Items.Add(aggregate);
    public void Update(MasterDataItem aggregate) { }
    public void Remove(MasterDataItem aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryLocationLogs : IDriverLocationLogRepository
{
    public List<DriverLocationLog> Items { get; } = [];

    public Task<int> DeleteRecordedBeforeAsync(DateTimeOffset cutoff, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.RemoveAll(l => l.RecordedAt < cutoff));

    public Task<DriverLocationLog?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(l => l.Id == id));

    public void Add(DriverLocationLog aggregate) => Items.Add(aggregate);
    public void Update(DriverLocationLog aggregate) { }
    public void Remove(DriverLocationLog aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryRefunds : IRefundRepository
{
    public List<Refund> Items { get; } = [];

    public Task<bool> HasOpenComplaintAsync(long orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(r => r.OrderId == orderId && r.SourceType == RefundSourceType.Complaint
            && r.RefundStatus is RefundStatus.Requested or RefundStatus.Approved));

    public Task<bool> HasOpenRefundForIncidentAsync(long incidentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(r => r.IncidentId == incidentId && r.RefundStatus is RefundStatus.Requested or RefundStatus.Approved));

    public Task<Refund?> GetOpenForIncidentAsync(long incidentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.LastOrDefault(r => r.IncidentId == incidentId && r.RefundStatus is RefundStatus.Requested or RefundStatus.Approved));

    public Task<List<Refund>> GetOpenForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items
            .Where(r => r.OrderId == orderId && r.RefundStatus is RefundStatus.Requested or RefundStatus.Approved)
            .ToList());

    public Task<decimal> GetClaimedTotalForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items
            .Where(r => r.OrderId == orderId && r.RefundStatus is RefundStatus.Requested or RefundStatus.Approved or RefundStatus.Processed)
            .Sum(r => r.ProposedAmount ?? r.RequestedAmount));

    /// <summary>Customers whose orders have an open refund; refunds here do not know their order's customer.</summary>
    public HashSet<long> CustomersWithOpenRefunds { get; } = [];

    public Task<bool> HasOpenForCustomerAsync(long customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(CustomersWithOpenRefunds.Contains(customerId));

    public Task<Refund?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public void Add(Refund aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(Refund aggregate) { }
    public void Remove(Refund aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryRatings : IRatingRepository
{
    public List<Rating> Items { get; } = [];

    public Task<bool> ExistsForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(r => r.OrderId == orderId));

    public Task<Rating?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(r => r.Id == id));

    public void Add(Rating aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(Rating aggregate) { }
    public void Remove(Rating aggregate) => Items.Remove(aggregate);
}

internal sealed class InMemoryIncidents : IIncidentRepository
{
    public List<InTransitIncident> Items { get; } = [];

    public Task<InTransitIncident?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == id));

    public void Add(InTransitIncident aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }

    public void Update(InTransitIncident aggregate) { }
    public void Remove(InTransitIncident aggregate) => Items.Remove(aggregate);

    public Task<List<InTransitIncident>> GetOpenForOrderAsync(
        long orderId, long excludeIncidentId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items
            .Where(i => i.OrderId == orderId
                && i.Id != excludeIncidentId
                && i.ResolutionStatus is IncidentResolutionStatus.Open or IncidentResolutionStatus.InProgress)
            .ToList());

    public Task<bool> HasOpenReportedByAsync(long orderId, long reporterUserId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(i => i.OrderId == orderId && i.ReportedBy == reporterUserId && i.IsOpen));
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

    public Task<DriverProfile?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested(); // like the database call this stands in for
        return Task.FromResult(Items.FirstOrDefault(d => d.Id == id));
    }

    public Task<DriverProfile?> GetByIdWithApplicationsAndVehicleAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Id == id));

    public Task<DriverProfile?> GetByUserIdWithApplicationsAndVehicleAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.UserId == userId));

    public Task<DriverProfile?> GetByUserIdAsync(long userId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.UserId == userId));

    public Task<DriverProfile?> GetByApplicationIdWithApplicationsAndVehicleAsync(long applicationId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(d => d.Applications.Any(a => a.Id == applicationId)));

    public Task<IReadOnlyList<DriverProfile>> GetOnlineApprovedWithVehicleAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DriverProfile>>(
            Items.Where(d => d.ApprovalStatus == ApprovalStatus.Approved && d.OnlineStatus == OnlineStatus.Online).ToList());

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
    private static readonly System.Reflection.PropertyInfo WalletIdProperty =
        typeof(WalletTransaction).GetProperty(nameof(WalletTransaction.WalletId))!;

    public List<DriverWallet> Items { get; } = [];

    public Task<DriverWallet?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(w => w.Id == id));

    public Task<DriverWallet?> GetByDriverProfileIdAsync(long driverProfileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(w => w.DriverProfileId == driverProfileId));

    public Task<DriverWallet?> GetByDriverProfileIdWithTransactionsAsync(long driverProfileId, CancellationToken cancellationToken = default)
    {
        SyncTransactions();
        return Task.FromResult(Items.FirstOrDefault(w => w.DriverProfileId == driverProfileId));
    }

    public Task<DriverWallet?> GetByTransactionIdWithTransactionsAsync(long transactionId, CancellationToken cancellationToken = default)
    {
        SyncTransactions();
        return Task.FromResult(Items.FirstOrDefault(w => w.Transactions.Any(t => t.Id == transactionId)));
    }

    public void Add(DriverWallet aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
        SyncTransactions();
    }

    public void Update(DriverWallet aggregate) => SyncTransactions();
    public void Remove(DriverWallet aggregate) => Items.Remove(aggregate);

    internal void SyncTransactions()
    {
        var nextTxId = Items.SelectMany(w => w.Transactions).Select(t => t.Id).DefaultIfEmpty(0).Max() + 1;
        foreach (var wallet in Items)
        {
            foreach (var tx in wallet.Transactions)
            {
                if (tx.WalletId == 0 && wallet.Id != 0)
                    WalletIdProperty.SetValue(tx, wallet.Id);
                if (tx.Id == 0)
                    TestIds.Set(tx, nextTxId++);
            }
        }
    }
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

    internal Action? SyncGeneratedIds { get; set; }

    /// <summary>Runs right after a save, e.g. to act like the client disconnecting at that moment.</summary>
    public Action? AfterSave { get; set; }

    /// <summary>Pretends a unique index lost a race, like ApplicationDbContext turning a duplicate key into 409.</summary>
    public bool ThrowConflictOnSave { get; set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowConflictOnSave)
            throw new ModMove.Application.Common.Exceptions.ConflictException("A record with the same unique value already exists.");
        SyncGeneratedIds?.Invoke();
        Saves++;
        AfterSave?.Invoke();
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

    /// <summary>Set to true to act like the SMTP server being down.</summary>
    public bool Fail { get; set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new InvalidOperationException("SMTP is down");
        Sent.Add((to, subject, htmlBody));
        return Task.CompletedTask;
    }

    /// <summary>The 6-digit code in the last email (the OTP emails show it in bold).</summary>
    public string LastCode() => Regex.Match(Sent[^1].Body, "<b>([0-9]{6})</b>").Groups[1].Value;
}

internal sealed class CapturingPushNotifications : IPushNotificationService
{
    public List<(string Token, string Title, string Body, IReadOnlyDictionary<string, string>? Data)> Sent { get; } = [];

    /// <summary>Set to true to act like FCM being down.</summary>
    public bool Fail { get; set; }

    public Task SendAsync(
        string deviceToken, string title, string body,
        IReadOnlyDictionary<string, string>? data, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Fail)
            throw new HttpRequestException("FCM is down");
        Sent.Add((deviceToken, title, body, data));
        return Task.CompletedTask;
    }
}

/// <summary>Records SignalR pushes: Target is "user", "order" or "dispatchers"; Id is the user or order id.</summary>
internal sealed class CapturingRealtimeNotifier : IRealtimeNotifier
{
    public List<(string Target, string? Id, string EventName, object Payload)> Sent { get; } = [];

    /// <summary>Set to true to act like the SignalR backplane failing.</summary>
    public bool Fail { get; set; }

    /// <summary>Set to true to fail only sends to order groups.</summary>
    public bool FailOrderGroups { get; set; }

    public Task SendToOrderAsync(long orderId, string eventName, object payload, CancellationToken cancellationToken) =>
        Record("order", orderId.ToString(), eventName, payload, cancellationToken);

    public Task SendToUserAsync(long userId, string eventName, object payload, CancellationToken cancellationToken) =>
        Record("user", userId.ToString(), eventName, payload, cancellationToken);

    public Task SendToDispatchersAsync(string eventName, object payload, CancellationToken cancellationToken) =>
        Record("dispatchers", null, eventName, payload, cancellationToken);

    private Task Record(string target, string? id, string eventName, object payload, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Fail || (FailOrderGroups && target == "order"))
            throw new InvalidOperationException("SignalR is down");
        Sent.Add((target, id, eventName, payload));
        return Task.CompletedTask;
    }
}

/// <summary>Stands in for Google Maps: every address geocodes to a point, every trip has <see cref="Route"/>.</summary>
internal sealed class FakeMapService : IMapService
{
    public const string UnknownAddress = "nowhere";

    public RouteInfo Route { get; set; } = new(12m, 30);

    public Task<GeoPoint> GeocodeAsync(string address, CancellationToken cancellationToken) =>
        address == UnknownAddress
            ? throw new ModMove.Domain.Exceptions.DomainException("Address not found.", "ADDRESS_NOT_FOUND")
            : Task.FromResult(new GeoPoint(21.0285m, 105.8542m));

    public Task<RouteInfo> GetRouteAsync(GeoPoint from, GeoPoint to, CancellationToken cancellationToken) => Task.FromResult(Route);
}

/// <summary>Stands in for PayOS: every code gets a link and a QR; tests read what was asked and cancelled.</summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    public List<(long Code, decimal Amount, string Description, PaymentPlatform Platform, DateTimeOffset ExpiresAt)> Created { get; } = [];
    public List<long> Cancelled { get; } = [];
    public List<string> ConfirmedWebhooks { get; } = [];

    /// <summary>Set to true to act like PayOS being down.</summary>
    public bool Fail { get; set; }

    public Task<PaymentLink> CreatePaymentLinkAsync(
        long payosOrderCode, decimal amount, string description, PaymentPlatform platform,
        DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        if (Fail)
            throw new InvalidOperationException("PayOS is down");
        Created.Add((payosOrderCode, amount, description, platform, expiresAt));
        return Task.FromResult(new PaymentLink($"https://pay.payos.vn/web/{payosOrderCode}", $"qr-{payosOrderCode}"));
    }

    public const string ValidSignature = "valid-payos-signature";

    public Task CancelPaymentLinkAsync(long payosOrderCode, CancellationToken cancellationToken)
    {
        Cancelled.Add(payosOrderCode);
        return Task.CompletedTask;
    }

    public Task ConfirmWebhookAsync(string webhookUrl, CancellationToken cancellationToken = default)
    {
        if (Fail)
            throw new InvalidOperationException("PayOS is down");
        ConfirmedWebhooks.Add(webhookUrl);
        return Task.CompletedTask;
    }

    public bool VerifyWebhookSignature(string rawBody, string signature) =>
        !string.IsNullOrWhiteSpace(rawBody) && signature == ValidSignature;
}

internal sealed class InMemoryPaymentTransactions : IPaymentTransactionRepository
{
    public List<PaymentTransaction> Items { get; } = [];

    public Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForOrderAsync(long orderId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentTransaction>>(Items
            .Where(p => p.OrderId == orderId && p.PaymentMethod == PaymentMethod.PayOS && p.TransactionStatus == PaymentTransactionStatus.Pending)
            .ToList());

    public Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsForWalletAsync(long walletId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentTransaction>>(Items
            .Where(p => p.WalletId == walletId && p.PaymentMethod == PaymentMethod.PayOS && p.TransactionStatus == PaymentTransactionStatus.Pending)
            .ToList());

    public Task<IReadOnlyList<PaymentTransaction>> GetPendingPayOsTransactionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PaymentTransaction>>(Items
            .Where(p => p.PaymentMethod == PaymentMethod.PayOS && p.TransactionStatus == PaymentTransactionStatus.Pending)
            .ToList());

    public Task<PaymentTransaction?> GetByPayOsOrderCodeAsync(long payosOrderCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.PayosOrderCode == payosOrderCode));

    public Task<PaymentTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public void Add(PaymentTransaction aggregate)
    {
        if (aggregate.Id == 0)
            TestIds.Set(aggregate, Items.Count + 1);
        Items.Add(aggregate);
    }
    public void Update(PaymentTransaction aggregate) { }
    public void Remove(PaymentTransaction aggregate) => Items.Remove(aggregate);
}

/// <summary>Stands in for Cloudinary; the team's cloud is called "demo" here.</summary>
internal sealed class FakeFileStorage : IFileStorage
{
    public const string CloudName = "demo";

    public List<(string FileName, string Folder)> Uploaded { get; } = [];

    public Task<string> UploadImageAsync(Stream content, string fileName, string folder, CancellationToken cancellationToken)
    {
        Uploaded.Add((fileName, folder));
        return Task.FromResult($"https://res.cloudinary.com/{CloudName}/image/upload/{folder}/{Uploaded.Count}.jpg");
    }

    public bool IsOwnedUrl(string url) => url.StartsWith($"https://res.cloudinary.com/{CloudName}/", StringComparison.Ordinal);
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
    public InMemoryRefunds Refunds { get; } = new();
    public InMemoryDriverProfiles DriverProfiles { get; } = new();
    public InMemoryDriverWallets DriverWallets { get; } = new();
    public InMemoryRatings Ratings { get; } = new();
    public InMemoryIncidents Incidents { get; } = new();
    public InMemoryLocationLogs LocationLogs { get; } = new();
    public InMemoryMasterData MasterData { get; } = new();
    public CapturingRealtimeNotifier Realtime { get; } = new();
    public CapturingPushNotifications Push { get; } = new();
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public CapturingEmailSender Email { get; } = new();
    public FakeFileStorage Files { get; } = new();
    public FakeMapService Maps { get; } = new();
    public FakePaymentGateway Gateway { get; } = new();
    public InMemoryPaymentTransactions Payments { get; } = new();
    public InMemoryCoupons Coupons { get; } = new();
    public InMemoryRepository<VehicleType> VehicleTypes { get; } = new();
    public InMemoryPlatformSettings Settings { get; } = new();
    public FakeSql Sql { get; } = new();
    public IExcelExporter Excel { get; } = new ModMove.Infrastructure.ExternalServices.Excel.OpenXmlExcelExporter();
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
        UnitOfWork.SyncGeneratedIds = DriverWallets.SyncTransactions;
        Sender = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<IUserRepository>(Users)
            .AddSingleton<IOtpRepository>(Otps)
            .AddSingleton<IShippingOrderRepository>(Orders)
            .AddSingleton<IRefreshTokenRepository>(RefreshTokens)
            .AddSingleton<IRepository<SupportRequest>>(SupportRequests)
            .AddSingleton<IRepository<Refund>>(Refunds)
            .AddSingleton<IRefundRepository>(Refunds)
            .AddSingleton<IDriverProfileRepository>(DriverProfiles)
            .AddSingleton<IDriverWalletRepository>(DriverWallets)
            .AddSingleton<IRatingRepository>(Ratings)
            .AddSingleton<IIncidentRepository>(Incidents)
            .AddSingleton<IRepository<InTransitIncident>>(Incidents)
            .AddSingleton<IRepository<DriverLocationLog>>(LocationLogs)
            .AddSingleton<IDriverLocationLogRepository>(LocationLogs)
            .AddSingleton<IMasterDataRepository>(MasterData)
            .AddSingleton<IRealtimeNotifier>(Realtime)
            .AddSingleton<IPushNotificationService>(Push)
            .AddSingleton<IUnitOfWork>(UnitOfWork)
            .AddSingleton<IPasswordHasher>(new FakeHasher())
            .AddSingleton<IJwtTokenService>(new FakeTokens())
            .AddSingleton<IEmailSender>(Email)
            .AddSingleton<IFileStorage>(Files)
            .AddSingleton<IMapService>(Maps)
            .AddSingleton<IPaymentGateway>(Gateway)
            .AddSingleton<IRepository<PaymentTransaction>>(Payments)
            .AddSingleton<IPaymentTransactionRepository>(Payments)
            .AddSingleton<ICouponRepository>(Coupons)
            .AddSingleton<IRepository<VehicleType>>(VehicleTypes)
            .AddSingleton<IPlatformSettings>(Settings)
            .AddSingleton<ISqlConnectionFactory>(Sql)
            .AddSingleton<IExcelExporter>(Excel)
            .AddSingleton<ICurrentUserService>(new FakeCurrentUser(signedInUserId))
            .AddSingleton<TimeProvider>(Time)
            .BuildServiceProvider()
            .GetRequiredService<ISender>();
    }
}
