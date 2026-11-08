namespace ModMove.Domain.Constants;

/// <summary>Numeric limits from the SRS business rules (GB-xx). One place, so tests and code agree.</summary>
public static class BusinessRules
{
    public const int MaxFailedLogins = 5;                                   // GB-02
    public static readonly TimeSpan LoginLockDuration = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);  // GB-25
    public static readonly TimeSpan OtpResendWindow = TimeSpan.FromMinutes(15);
    public const int OtpMaxSendsPerWindow = 3;
    public const int OtpMaxFailedAttempts = 5; // wrong guesses before a code stops working

    public const int MaxCargoPhotos = 5;                                    // GB-06
    public const int MaxPhotoSizeMb = 5;
    public const int MinPickupPhotos = 2;                                   // GB-11
    public const int MaxPickupSubmissions = 5;                              // DECISIONS 6.3: re-sends after a rejection included
    public const int MaxRefundEvidencePhotos = 5;                           // DECISIONS 7.4

    public const decimal MinDepositTopUp = 50_000m;                         // DECISIONS 5.2 (VND)
    public static readonly TimeSpan RatingWindow = TimeSpan.FromDays(1);    // DECISIONS 7.5: after completion
    public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30); // DECISIONS 9.1

    // Values the Administrator can change (commission rate, loading fee, minimum COD deposit,
    // matching radius) live in platform_settings, not here (DECISIONS C8).

    public static readonly TimeSpan PayOsSessionLifetime = TimeSpan.FromMinutes(10); // GB-13
    public static readonly TimeSpan PayOsPaymentDeadline = TimeSpan.FromMinutes(30); // DECISIONS 3.1: an order still Unpaid this long after booking is cancelled (JOB-PAY-01)
    public const int PayOsMaxDescriptionLength = 9;                                  // PayOS non-linked bank transfer description limit
    public const long PayOsProbeOrderCode = 123L;                                    // PayOS confirm-webhook connectivity probe orderCode
    public static readonly TimeSpan MatchingTimeout = TimeSpan.FromMinutes(10);      // GB-24
    public static readonly TimeSpan AutoCompleteAfterDelivery = TimeSpan.FromHours(24); // GB-23
    public static readonly TimeSpan ComplaintWindow = TimeSpan.FromHours(24);        // GB-18
    public static readonly TimeSpan GpsLogRetention = TimeSpan.FromDays(30);         // JOB-GPS-01
    public static readonly TimeSpan GpsDisconnectAfter = TimeSpan.FromMinutes(5);    // DECISIONS 6.4, GB-39: no GPS for this long = disconnected
}
