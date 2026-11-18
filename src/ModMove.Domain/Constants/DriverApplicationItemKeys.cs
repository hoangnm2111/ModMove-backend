namespace ModMove.Domain.Constants;

/// <summary>
/// Canonical item keys for structured driver application rejections and partial resubmissions (D2).
/// </summary>
public static class DriverApplicationItemKeys
{
    public const string LicenseFront = "LicenseFront";
    public const string LicenseBack = "LicenseBack";
    public const string VehicleRegistration = "VehicleRegistration";
    public const string VehicleInspection = "VehicleInspection";
    public const string VehiclePhoto = "VehiclePhoto";
    public const string CitizenIdFront = "CitizenIdFront";
    public const string CitizenIdBack = "CitizenIdBack";
    public const string Portrait = "Portrait";
    public const string VehicleInfo = "VehicleInfo";
    public const string PersonalInfo = "PersonalInfo";

    /// <summary>Maps each document item key to its fixed 0-based position in <c>DriverApplication.DocumentUrls</c> (D2).</summary>
    public static readonly IReadOnlyDictionary<string, int> DocumentIndexByKey =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [LicenseFront] = 0,
            [LicenseBack] = 1,
            [VehicleRegistration] = 2,
            [VehicleInspection] = 3,
            [VehiclePhoto] = 4,
            [CitizenIdFront] = 5,
            [CitizenIdBack] = 6,
        };

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(
            DocumentIndexByKey.Keys.Concat([Portrait, VehicleInfo, PersonalInfo]),
            StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Inspection"] = VehicleInspection,
            ["InspectionCert"] = VehicleInspection,
            ["InspectionCertificate"] = VehicleInspection,
            ["Registration"] = VehicleRegistration,
            ["RegistrationCert"] = VehicleRegistration,
            ["CccdFront"] = CitizenIdFront,
            ["CccdBack"] = CitizenIdBack,
            ["DriverLicenseFront"] = LicenseFront,
            ["DriverLicenseBack"] = LicenseBack,
        };

    public static string? Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var trimmed = key.Trim().Replace("_", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        var match = All.FirstOrDefault(k => string.Equals(k, trimmed, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return match;

        return Aliases.TryGetValue(trimmed, out var aliased) ? aliased : null;
    }
}

