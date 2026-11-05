using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace ModMove.Infrastructure.ExternalServices.Firebase;

/// <summary>
/// Firebase Cloud Messaging (HTTP v1 API) service-account configuration (DECISIONS C3, 3.4, B17).
/// Supports either individual keys (<c>Firebase:ProjectId</c>, <c>ClientEmail</c>, <c>PrivateKey</c>),
/// an inline service-account JSON (<c>Firebase:CredentialsJson</c>), or a file path (<c>Firebase:CredentialsPath</c>).
/// </summary>
public sealed class FirebaseOptions
{
    public string ProjectId { get; init; } = "";
    public string ClientEmail { get; init; } = "";
    public string PrivateKey { get; init; } = "";
    public string Endpoint { get; init; } = "https://fcm.googleapis.com";
    public string TokenEndpoint { get; init; } = "https://oauth2.googleapis.com/token";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(ClientEmail)
        && !string.IsNullOrWhiteSpace(PrivateKey);

    public static FirebaseOptions? TryLoad(IConfiguration configuration)
    {
        var projectId = configuration["Firebase:ProjectId"]?.Trim() ?? "";
        var clientEmail = configuration["Firebase:ClientEmail"]?.Trim() ?? "";
        var privateKey = configuration["Firebase:PrivateKey"] ?? "";
        var credentialsJson = configuration["Firebase:CredentialsJson"];
        var credentialsPath = configuration["Firebase:CredentialsPath"];
        var endpoint = configuration["Firebase:Endpoint"] is { Length: > 0 } ep ? ep.TrimEnd('/') : "https://fcm.googleapis.com";
        var tokenEndpoint = configuration["Firebase:TokenEndpoint"] is { Length: > 0 } tep ? tep.Trim() : "https://oauth2.googleapis.com/token";

        var anySet = !string.IsNullOrWhiteSpace(projectId)
            || !string.IsNullOrWhiteSpace(clientEmail)
            || !string.IsNullOrWhiteSpace(privateKey)
            || !string.IsNullOrWhiteSpace(credentialsJson)
            || !string.IsNullOrWhiteSpace(credentialsPath);

        if (!anySet)
            return null;

        if (string.IsNullOrWhiteSpace(credentialsJson) && !string.IsNullOrWhiteSpace(credentialsPath))
        {
            if (!File.Exists(credentialsPath))
                throw new InvalidOperationException($"Firebase:CredentialsPath file was not found: '{credentialsPath}'.");
            credentialsJson = File.ReadAllText(credentialsPath);
        }

        if (!string.IsNullOrWhiteSpace(credentialsJson))
        {
            using var doc = JsonDocument.Parse(credentialsJson);
            var root = doc.RootElement;
            if (string.IsNullOrWhiteSpace(projectId) && root.TryGetProperty("project_id", out var pid))
                projectId = pid.GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(clientEmail) && root.TryGetProperty("client_email", out var ce))
                clientEmail = ce.GetString()?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(privateKey) && root.TryGetProperty("private_key", out var pk))
                privateKey = pk.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(configuration["Firebase:TokenEndpoint"])
                && root.TryGetProperty("token_uri", out var tu)
                && tu.GetString() is { Length: > 0 } tokenUri)
            {
                tokenEndpoint = tokenUri.Trim();
            }
        }

        privateKey = privateKey.Replace("\\n", "\n", StringComparison.Ordinal).Trim();

        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(clientEmail) || string.IsNullOrWhiteSpace(privateKey))
        {
            throw new InvalidOperationException(
                "Firebase is partially configured: provide ProjectId, ClientEmail and PrivateKey (or CredentialsJson / CredentialsPath).");
        }

        return new FirebaseOptions
        {
            ProjectId = projectId,
            ClientEmail = clientEmail,
            PrivateKey = privateKey,
            Endpoint = endpoint,
            TokenEndpoint = tokenEndpoint,
        };
    }
}

