using Microsoft.Extensions.Configuration;

namespace ModMove.Infrastructure.Persistence.Seed;

/// <summary>
/// The first Administrator in Production, where demo accounts never run. Read from <c>Seed:Admin:*</c> (environment
/// variables on the server) and used only while no Administrator exists, so the password can be removed after the
/// first start. A class, not a record: a record's ToString would print the password.
/// </summary>
public sealed class InitialAdministrator
{
    // Same rules as sign-up (AuthRules.ValidPassword and ValidPhone).
    private const int MinPasswordLength = 8;

    private InitialAdministrator(string fullName, string phoneNumber, string email, string password)
    {
        FullName = fullName;
        PhoneNumber = phoneNumber;
        Email = email;
        Password = password;
    }

    public string FullName { get; }
    public string PhoneNumber { get; }
    public string Email { get; }
    public string Password { get; }

    public override string ToString() => $"InitialAdministrator {{ Email = {Email} }}";

    /// <summary>Null when <c>Seed:Admin</c> is not set; throws when it is set but incomplete or weak.</summary>
    public static InitialAdministrator? FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Seed:Admin");
        var email = section["Email"]?.Trim() ?? "";
        var phone = section["PhoneNumber"]?.Trim() ?? "";
        var password = section["Password"] ?? "";
        if (email.Length == 0 && password.Length == 0)
            return null;

        if (!email.Contains('@'))
            throw new InvalidOperationException("Seed:Admin:Email must be an email address.");
        if (phone.Length != 10 || phone[0] != '0' || !phone.All(char.IsAsciiDigit))
            throw new InvalidOperationException("Seed:Admin:PhoneNumber must be 10 digits starting with 0.");
        if (password.Length < MinPasswordLength || !password.Any(char.IsAsciiLetter) || !password.Any(char.IsAsciiDigit))
            throw new InvalidOperationException($"Seed:Admin:Password needs at least {MinPasswordLength} characters with a letter and a digit.");

        var fullName = section["FullName"]?.Trim() is { Length: > 0 } name ? name : "Administrator";
        return new InitialAdministrator(fullName, phone, email, password);
    }
}
