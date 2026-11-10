namespace ModMove.WebApi.Services;

/// <summary>
/// Development runs without external keys (stand-ins fail only when called). Production must not: a missing key would
/// first show as a 500 on a booking, an upload or a sign-up email, so the app refuses to start and names what is missing.
/// </summary>
public static class ProductionConfiguration
{
    // Firebase stays optional until the mobile app is released: without it pushes are only logged.
    private static readonly string[] RequiredKeys =
    [
        "MapVina:ApiKey",
        "Cloudinary:CloudName", "Cloudinary:ApiKey", "Cloudinary:ApiSecret",
        "PayOS:ClientId", "PayOS:ReturnUrls:Web", "PayOS:CancelUrls:Web",
        "Email:Host", "Email:UserName", "Email:Password",
    ];

    public static void EnsureComplete(IConfiguration configuration)
    {
        var missing = RequiredKeys.Where(key => string.IsNullOrWhiteSpace(configuration[key])).ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException($"Production configuration is missing: {string.Join(", ", missing)}.");
    }
}
