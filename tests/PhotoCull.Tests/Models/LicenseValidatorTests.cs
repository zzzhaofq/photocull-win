using PhotoCull.Models;
using Xunit;

namespace PhotoCull.Tests.Models;

public class LicenseValidatorTests
{
    [Fact]
    public void ValidPermanentLicense()
    {
        var code = LicenseValidator.Generate("permanent");
        var result = LicenseValidator.Validate(code, checkActivationWindow: false);
        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(LicenseType.Permanent, result.Type);
    }

    [Fact]
    public void ValidTrialLicense()
    {
        var code = LicenseValidator.Generate("trial");
        var result = LicenseValidator.Validate(code, checkActivationWindow: false);
        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(LicenseType.Trial, result.Type);
        Assert.NotNull(result.DaysLeft);
        Assert.True(result.DaysLeft >= 29);
    }

    [Fact]
    public void InvalidFormatEmpty()
    {
        var result = LicenseValidator.Validate("");
        Assert.Equal(LicenseStatus.Invalid, result.Status);
    }

    [Fact]
    public void InvalidFormatNoDot()
    {
        var result = LicenseValidator.Validate("nodothere");
        Assert.Equal(LicenseStatus.Invalid, result.Status);
    }

    [Fact]
    public void InvalidFormatBadBase64()
    {
        var result = LicenseValidator.Validate("not-valid-base64.fakesig");
        Assert.Equal(LicenseStatus.Invalid, result.Status);
    }

    [Fact]
    public void TamperedSignature()
    {
        var code = LicenseValidator.Generate("permanent");
        var parts = code.Split('.');
        Assert.Equal(2, parts.Length);
        var tampered = $"{parts[0]}.TAMPERED_SIGNATURE";
        var result = LicenseValidator.Validate(tampered);
        Assert.Equal(LicenseStatus.Invalid, result.Status);
    }

    [Fact]
    public void ExpiredLicense()
    {
        var payload = new LicensePayload { Type = "trial", Exp = "2020-01-01", Iat = "2019-12-01T00:00:00Z" };
        var payloadBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);
        var signature = LicenseValidator.Sign(payloadBytes);
        var code = $"{payloadBase64}.{signature}";

        var result = LicenseValidator.Validate(code, checkActivationWindow: false);
        Assert.Equal(LicenseStatus.Expired, result.Status);
    }

    [Fact]
    public void ActivationWindowExpired()
    {
        var expDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");
        var oldIat = DateTime.UtcNow.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var payload = new LicensePayload { Type = "trial", Exp = expDate, Iat = oldIat };
        var payloadBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);
        var signature = LicenseValidator.Sign(payloadBytes);
        var code = $"{payloadBase64}.{signature}";

        var result = LicenseValidator.Validate(code, checkActivationWindow: true);
        Assert.Equal(LicenseStatus.ActivationWindowExpired, result.Status);
    }

    [Fact]
    public void SkipActivationWindowCheck()
    {
        var expDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd");
        var oldIat = DateTime.UtcNow.AddHours(-13).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var payload = new LicensePayload { Type = "trial", Exp = expDate, Iat = oldIat };
        var payloadBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);
        var signature = LicenseValidator.Sign(payloadBytes);
        var code = $"{payloadBase64}.{signature}";

        var result = LicenseValidator.Validate(code, checkActivationWindow: false);
        Assert.Equal(LicenseStatus.Valid, result.Status);
        Assert.Equal(LicenseType.Trial, result.Type);
    }

    [Fact]
    public void SignatureConsistency()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("test payload data");
        var sig1 = LicenseValidator.Sign(data);
        var sig2 = LicenseValidator.Sign(data);
        Assert.Equal(sig1, sig2);

        var differentData = System.Text.Encoding.UTF8.GetBytes("different payload");
        var sig3 = LicenseValidator.Sign(differentData);
        Assert.NotEqual(sig1, sig3);
    }
}
