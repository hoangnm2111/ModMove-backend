using Microsoft.Extensions.DependencyInjection;
using MediatR;
using ModMove.Application;
using ModMove.Application.Common.Exceptions;
using ModMove.Application.Common.Interfaces;
using ModMove.Application.Common.Interfaces.Persistence;
using ModMove.Application.Features.Auth.Commands.Login;
using ModMove.Domain.Entities;
using ModMove.Domain.Enums;

namespace ModMove.IntegrationTests;

/// <summary>Runs the real MediatR pipeline (validation + logging + handler) with in-memory fakes for the ports.</summary>
public class LoginTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 8, 0, 0, TimeSpan.Zero);

    private sealed class FakeUsers(params User[] users) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
            Task.FromResult(users.FirstOrDefault(u => u.Email == email));

        public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
            Task.FromResult(users.FirstOrDefault(u => u.Id == id));

        public void Add(User aggregate) { }
        public void Update(User aggregate) { }
        public void Remove(User aggregate) { }
    }

    private sealed class CountingUnitOfWork : IUnitOfWork
    {
        public int Saves { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.FromResult(1);
        }
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "h:" + password;

        public bool Verify(string password, string passwordHash) => passwordHash == "h:" + password;
    }

    private sealed class FakeTokens : IJwtTokenService
    {
        public string CreateAccessToken(long userId, UserRole role) => $"token-{role}";
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static (ISender Sender, CountingUnitOfWork Uow) Build(User user)
    {
        var uow = new CountingUnitOfWork();
        var services = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<IUserRepository>(new FakeUsers(user))
            .AddSingleton<IUnitOfWork>(uow)
            .AddSingleton<IPasswordHasher>(new FakeHasher())
            .AddSingleton<IJwtTokenService>(new FakeTokens())
            .AddSingleton<TimeProvider>(new FixedTime());
        return (services.BuildServiceProvider().GetRequiredService<ISender>(), uow);
    }

    private static User NewUser(UserRole role = UserRole.Customer) =>
        User.Create("A", "0900000001", "a@x.vn", "h:secret", role);

    [Fact]
    public async Task Valid_credentials_return_a_token_for_the_users_role()
    {
        var (sender, uow) = Build(NewUser(UserRole.Driver));

        var response = await sender.Send(new LoginCommand("a@x.vn", "secret"));

        Assert.Equal("token-Driver", response.AccessToken);
        Assert.Equal(UserRole.Driver, response.Role);
        Assert.Equal(1, uow.Saves);
    }

    [Fact]
    public async Task Wrong_password_is_unauthorized_and_counts_as_a_failure_GB02()
    {
        var user = NewUser();
        var (sender, uow) = Build(user);

        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "nope")));

        Assert.Equal(1, user.FailedLoginCount);
        Assert.Equal(1, uow.Saves); // the failure is saved before the 401 is thrown
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_unauthorized_answer()
    {
        var (sender, uow) = Build(NewUser());

        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("nobody@x.vn", "secret")));

        Assert.Equal(0, uow.Saves);
    }

    [Fact]
    public async Task Five_failures_lock_the_account_even_for_the_right_password_GB02()
    {
        var user = NewUser();
        var (sender, _) = Build(user);

        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "nope")));

        Assert.True(user.IsLocked(Now));
        await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "secret")));
    }

    [Fact]
    public async Task Locked_account_and_unknown_email_get_the_same_answer()
    {
        var user = NewUser();
        var (sender, _) = Build(user);
        for (var i = 0; i < 5; i++)
            await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "nope")));

        var locked = await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("a@x.vn", "secret")));
        var unknown = await Assert.ThrowsAsync<UnauthorizedException>(() => sender.Send(new LoginCommand("nobody@x.vn", "secret")));

        Assert.Equal(unknown.Message, locked.Message);
    }

    [Fact]
    public async Task Inactive_account_cannot_sign_in()
    {
        var user = NewUser();
        user.ChangeStatus(AccountStatus.Inactive);
        var (sender, _) = Build(user);

        await Assert.ThrowsAsync<ForbiddenAccessException>(() => sender.Send(new LoginCommand("a@x.vn", "secret")));
    }

    [Fact]
    public async Task Empty_email_fails_validation_before_reaching_the_handler()
    {
        var (sender, uow) = Build(NewUser());

        await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new LoginCommand("", "secret")));

        Assert.Equal(0, uow.Saves);
    }
}
