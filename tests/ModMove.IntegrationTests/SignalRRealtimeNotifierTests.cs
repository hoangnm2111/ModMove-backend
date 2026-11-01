using Microsoft.AspNetCore.SignalR;
using ModMove.WebApi.Hubs;

namespace ModMove.IntegrationTests;

/// <summary>DECISIONS B12, B17: who receives each server push from /hubs/tracking.</summary>
public class SignalRRealtimeNotifierTests
{
    private sealed class RecordingClients : IHubClients
    {
        public List<(string Target, string Method, object?[] Args)> Calls { get; } = [];

        private IClientProxy Proxy(string target) => new Recorder(this, target);
        public IClientProxy All => Proxy("all");
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => Proxy("all-except");
        public IClientProxy Client(string connectionId) => Proxy("connection:" + connectionId);
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => Proxy("connections");
        public IClientProxy Group(string groupName) => Proxy("group:" + groupName);
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => Proxy("group-except:" + groupName);
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => Proxy("groups");
        public IClientProxy User(string userId) => Proxy("user:" + userId);
        public IClientProxy Users(IReadOnlyList<string> userIds) => Proxy("users");

        private sealed class Recorder(RecordingClients owner, string target) : IClientProxy
        {
            public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
            {
                owner.Calls.Add((target, method, args));
                return Task.CompletedTask;
            }
        }
    }

    private sealed class FakeHubContext(RecordingClients clients) : IHubContext<TrackingHub>
    {
        public IHubClients Clients { get; } = clients;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private static (SignalRRealtimeNotifier Notifier, RecordingClients Clients) Build()
    {
        var clients = new RecordingClients();
        return (new SignalRRealtimeNotifier(new FakeHubContext(clients)), clients);
    }

    [Fact]
    public async Task User_events_go_to_every_connection_of_that_user_not_a_custom_group()
    {
        var (notifier, clients) = Build();

        await notifier.SendToUserAsync(42, "NotificationReceived", "payload", CancellationToken.None);

        var call = Assert.Single(clients.Calls);
        Assert.Equal(("user:42", "NotificationReceived"), (call.Target, call.Method));
        Assert.Equal(["payload"], call.Args);
    }

    [Fact]
    public async Task Order_events_go_to_the_order_group()
    {
        var (notifier, clients) = Build();

        await notifier.SendToOrderAsync(7, "LocationUpdated", "payload", CancellationToken.None);

        Assert.Equal("group:order-7", Assert.Single(clients.Calls).Target);
    }

    [Fact]
    public async Task Dispatcher_events_go_to_the_dispatchers_group()
    {
        var (notifier, clients) = Build();

        await notifier.SendToDispatchersAsync("LocationUpdated", "payload", CancellationToken.None);

        Assert.Equal("group:dispatchers", Assert.Single(clients.Calls).Target);
    }
}
