namespace ModMove.Infrastructure.ExternalServices.Cloudinary;

/// <summary>Bound from the "Cloudinary" section; the secret lives in appsettings.Development.json / user secrets, never in the repo.</summary>
public sealed class CloudinaryOptions
{
    public string CloudName { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string ApiSecret { get; init; } = "";

    /// <summary>Signed preset created in the Cloudinary console (folder modmove, unguessable public ids).</summary>
    public string UploadPreset { get; init; } = "modmove";
}
