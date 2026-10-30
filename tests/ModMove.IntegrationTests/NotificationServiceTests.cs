using Microsoft.Extensions.DependencyInjection;
using ModMove.Application;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>DECISIONS B17: one call sends FCM to the app and SignalR NotificationReceived to the user's open connections.</summary>
public class NotificationServiceTests
{
    private static readonly Notification Accepted = new("ORDER_ACCEPTED", "Đơn đã có tài xế", "Tài xế đang đến điểm lấy hàng.",
        new Dictionary<string, string> { ["orderId"] = "7" });

    private static User Customer(string? fcmToken)
    {
        var user = User.Create("Khách", "0900000001", "khach@x.vn", "hash", UserRole.Customer, emailVerified: true);
        user.SetFcmToken(fcmToken);
        return user;
    }

    private static INotificationService Build(User user, CapturingRealtimeNotifier realtime, IPushNotificationService? push) =>
        Build(new InMemoryUsers(user), realtime, push);

    private static INotificationService Build(IUserRepository users, CapturingRealtimeNotifier realtime, IPushNotificationService? push)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton(users)
            .AddSingleton<IRealtimeNotifier>(realtime);
        if (push is not null)
            services.AddSingleton(push);
        return services.BuildServiceProvider().GetRequiredService<INotificationService>();
    }

    [Fact]
    public async Task Sends_NotificationReceived_to_every_connection_of_the_user_B17()
    {
        var user = Customer(null);
        var realtime = new CapturingRealtimeNotifier();

        await Build(user, realtime, new CapturingPushNotifications()).NotifyAsync(user.Id, Accepted, CancellationToken.None);

        var sent = Assert.Single(realtime.Sent);
        Assert.Equal(("user", user.Id.ToString(), RealtimeEvents.NotificationReceived), (sent.Target, sent.Id, sent.EventName));
        Assert.Equal(Accepted, sent.Payload);
    }

    [Fact]
    public async Task Pushes_to_the_device_token_of_the_user_with_the_event_type()
    {
        var user = Customer("device-1");
        var push = new CapturingPushNotifications();

        await Build(user, new CapturingRealtimeNotifier(), push).NotifyAsync(user.Id, Accepted, CancellationToken.None);

        var sent = Assert.Single(push.Sent);
        Assert.Equal(("device-1", Accepted.Title, Accepted.Body), (sent.Token, sent.Title, sent.Body));
        Assert.Equal("ORDER_ACCEPTED", sent.Data!["type"]);
        Assert.Equal("7", sent.Data["orderId"]);
    }

    [Fact]
    public async Task No_push_when_the_user_has_no_device_token()
    {
        var user = Customer(null);
        var push = new CapturingPushNotifications();

        await Build(user, new CapturingRealtimeNotifier(), push).NotifyAsync(user.Id, Accepted, CancellationToken.None);

        Assert.Empty(push.Sent);
    }

    [Fact]
    public async Task Works_before_the_FCM_adapter_exists()
    {
        var user = Customer("device-1");
        var realtime = new CapturingRealtimeNotifier();

        await Build(user, realtime, push: null).NotifyAsync(user.Id, Accepted, CancellationToken.None);

        Assert.Single(realtime.Sent);
    }

    [Fact]
    public async Task A_failed_delivery_does_not_fail_the_action_that_was_already_saved()
    {
        var user = Customer("device-1");
        var realtime = new CapturingRealtimeNotifier { Fail = true };
        var push = new CapturingPushNotifications { Fail = true };

        await Build(user, realtime, push).NotifyAsync(user.Id, Accepted, CancellationToken.None);

        Assert.Empty(realtime.Sent);
        Assert.Empty(push.Sent);
    }

    private sealed class BrokenUsers : IUserRepository
    {
        private static Exception Down() => new InvalidOperationException("database is down");
        public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) => throw Down();
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw Down();
        public Task<bool> IsEmailOrPhoneTakenAsync(string email, string phoneNumber, CancellationToken cancellationToken = default) => throw Down();
        public Task<bool> IsPhoneTakenByAnotherAsync(string phoneNumber, long userId, CancellationToken cancellationToken = default) => throw Down();
        public Task ReleaseFcmTokenAsync(string token, long keepForUserId, CancellationToken cancellationToken = default) => throw Down();
        public void Add(User aggregate) => throw Down();
        public void Update(User aggregate) => throw Down();
        public void Remove(User aggregate) => throw Down();
    }

    [Fact]
    public async Task Failing_to_load_the_device_token_does_not_fail_the_action()
    {
        var realtime = new CapturingRealtimeNotifier();

        await Build(new BrokenUsers(), realtime, new CapturingPushNotifications()).NotifyAsync(1, Accepted, CancellationToken.None);

        Assert.Single(realtime.Sent);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed()
    {
        var user = Customer("device-1");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Build(user, new CapturingRealtimeNotifier(), new CapturingPushNotifications()).NotifyAsync(user.Id, Accepted, cts.Token));
    }
}
