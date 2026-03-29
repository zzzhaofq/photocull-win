using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PhotoCull.Models;

public class LicensePayload
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("exp")]
    public string Exp { get; set; } = string.Empty;

    [JsonPropertyName("iat")]
    public string Iat { get; set; } = string.Empty;
}

public record LicenseValidationResult(LicenseStatus Status, LicenseType? Type = null, int? DaysLeft = null);

public static class LicenseValidator
{
    public static readonly string Secret = "PhotoCull-2024-SecretKey-X9k2mP";
    public static readonly TimeSpan ActivationWindow = TimeSpan.FromMinutes(15);

    public static LicenseValidationResult Validate(string code, bool checkActivationWindow = true)
    {
        var trimmed = code.Trim();
        var parts = trimmed.Split('.');
        if (parts.Length != 2)
            return new LicenseValidationResult(LicenseStatus.Invalid);

        byte[]? payloadData;
        try
        {
            payloadData = Convert.FromBase64String(parts[0]);
        }
        catch
        {
            return new LicenseValidationResult(LicenseStatus.Invalid);
        }

        // Verify signature
        var expectedSignature = Sign(payloadData);
        if (expectedSignature != parts[1])
            return new LicenseValidationResult(LicenseStatus.Invalid);

        // Parse payload
        LicensePayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<LicensePayload>(payloadData);
        }
        catch
        {
            return new LicenseValidationResult(LicenseStatus.Invalid);
        }

        if (payload == null)
            return new LicenseValidationResult(LicenseStatus.Invalid);

        // Check type
        LicenseType licenseType;
        switch (payload.Type)
        {
            case "trial":
                licenseType = LicenseType.Trial;
                break;
            case "permanent":
                licenseType = LicenseType.Permanent;
                break;
            default:
                return new LicenseValidationResult(LicenseStatus.Invalid);
        }

        // Check activation window
        if (checkActivationWindow)
        {
            var iatDate = ParseIat(payload.Iat);
            if (iatDate.HasValue)
            {
                var elapsed = DateTime.UtcNow - iatDate.Value;
                if (elapsed > ActivationWindow)
                    return new LicenseValidationResult(LicenseStatus.ActivationWindowExpired);
            }
        }

        // Check expiration
        if (!DateTime.TryParseExact(payload.Exp, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var expDate))
        {
            return new LicenseValidationResult(LicenseStatus.Invalid);
        }

        if (expDate < DateTime.UtcNow)
            return new LicenseValidationResult(LicenseStatus.Expired);

        int? daysLeft = licenseType == LicenseType.Trial
            ? (int)(expDate - DateTime.UtcNow).TotalDays
            : null;

        return new LicenseValidationResult(LicenseStatus.Valid, licenseType, daysLeft);
    }

    private static DateTime? ParseIat(string iat)
    {
        // ISO 8601 format
        if (DateTime.TryParse(iat, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var isoDate))
        {
            return isoDate;
        }

        // yyyy-MM-dd fallback
        if (DateTime.TryParseExact(iat, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var dayDate))
        {
            return dayDate;
        }

        return null;
    }

    public static string Sign(byte[] payloadBytes)
    {
        var keyBytes = Encoding.UTF8.GetBytes(Secret);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(payloadBytes);
        return Convert.ToBase64String(hash);
    }

    public static string Generate(string type)
    {
        var exp = type switch
        {
            "trial" => DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
            "permanent" => "9999-12-31",
            _ => throw new ArgumentException($"Unknown license type: {type}")
        };

        var iat = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

        var payload = new LicensePayload { Type = type, Exp = exp, Iat = iat };
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);
        var signature = Sign(payloadBytes);

        return $"{payloadBase64}.{signature}";
    }
}
