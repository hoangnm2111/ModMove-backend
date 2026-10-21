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

internal sealed class InMemoryUsers(params User[] seed) : IUserRepository
{
    public List<User> Items { get; } = [.. seed];

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

    public Task<bool> IsEmailOrPhoneTakenAsync(string email, string phoneNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.Any(u => u.Email == email || u.PhoneNumber == phoneNumber));

    public Task<User?> GetByIdAsync(long id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

    public void Add(User aggregate) => Items.Add(aggregate);
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
    public CountingUnitOfWork UnitOfWork { get; } = new();
    public CapturingEmailSender Email { get; } = new();
    public MutableTime Time { get; } = new(Start);
    public ISender Sender { get; }

    public TestApp(long? signedInUserId = null, params User[] users)
    {
        Users = new InMemoryUsers(users);
        Sender = new ServiceCollection()
            .AddLogging()
            .AddApplication()
            .AddSingleton<IUserRepository>(Users)
            .AddSingleton<IOtpRepository>(Otps)
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
