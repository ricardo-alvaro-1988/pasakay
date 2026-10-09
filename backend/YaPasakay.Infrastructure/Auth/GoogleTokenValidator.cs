using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class GoogleTokenValidator(
    IOptions<GoogleAuthOptions> options,
    ILogger<GoogleTokenValidator> logger)
{
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    public async Task<(bool Ok, string? Error, GoogleProfile? Profile)> ValidateAsync(string? idToken)
    {
        var clientId = NormalizeClientId(options.Value.ClientId);
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return (false, "Google sign-in is not configured. Add GoogleAuth:ClientId to appsettings.", null);
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return (false, "Google sign-in was cancelled.", null);
        }

        var audiences = new HashSet<string>(StringComparer.Ordinal)
        {
            clientId,
        };
        foreach (var extra in options.Value.AdditionalClientIds ?? [])
        {
            var value = NormalizeClientId(extra);
            if (!string.IsNullOrWhiteSpace(value))
            {
                audiences.Add(value);
            }
        }

        // 1) Local JWKS validation (Google.Apis.Auth)
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = audiences,
                    IssuedAtClockTolerance = TimeSpan.FromMinutes(5),
                    ExpirationTimeClockTolerance = TimeSpan.FromMinutes(5),
                });
            return FromPayload(
                payload.Subject,
                payload.Email,
                payload.EmailVerified == true,
                payload.GivenName,
                payload.FamilyName,
                payload.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Google JWKS validation failed. configuredAudience={Configured} tokenAud={TokenAud}",
                string.Join(',', audiences),
                TryReadAudience(idToken) ?? "(unknown)");
        }

        // 2) Fallback: Google tokeninfo (works when local cert fetch/clock quirks fail)
        try
        {
            var profile = await ValidateViaTokenInfoAsync(idToken, audiences);
            if (profile is not null)
            {
                return (true, null, profile);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google tokeninfo validation failed.");
        }

        var tokenAud = TryReadAudience(idToken);
        if (!string.IsNullOrWhiteSpace(tokenAud) &&
            !tokenAud.Split(',').Any(a => audiences.Contains(a.Trim())))
        {
            logger.LogWarning(
                "Google ID token audience mismatch. configured={Configured} tokenAud={TokenAud}",
                string.Join(',', audiences),
                tokenAud);
            return (
                false,
                "Google sign-in could not be verified (client mismatch). Check GoogleAuth__ClientId matches the OAuth Web client.",
                null);
        }

        return (false, "Google sign-in could not be verified. Refresh and try again.", null);
    }

    private async Task<GoogleProfile?> ValidateViaTokenInfoAsync(string idToken, HashSet<string> audiences)
    {
        using var response = await Http.GetAsync(
            "https://oauth2.googleapis.com/tokeninfo?id_token=" + Uri.EscapeDataString(idToken));
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Google tokeninfo HTTP {Status}", (int)response.StatusCode);
            return null;
        }

        var info = await response.Content.ReadFromJsonAsync<GoogleTokenInfo>();
        if (info is null)
        {
            return null;
        }

        var aud = (info.Aud ?? string.Empty).Trim();
        if (aud.Length == 0 || !audiences.Contains(aud))
        {
            logger.LogWarning(
                "Google tokeninfo audience rejected. configured={Configured} tokenAud={TokenAud}",
                string.Join(',', audiences),
                aud);
            return null;
        }

        var verified = string.Equals(info.EmailVerified, "true", StringComparison.OrdinalIgnoreCase)
            || info.EmailVerified == "1";
        var (ok, _, profile) = FromPayload(
            info.Sub,
            info.Email,
            verified,
            info.GivenName,
            info.FamilyName,
            info.Name);
        return ok ? profile : null;
    }

    private static (bool Ok, string? Error, GoogleProfile? Profile) FromPayload(
        string? subject,
        string? emailRaw,
        bool emailVerified,
        string? givenName,
        string? familyName,
        string? name)
    {
        if (!emailVerified)
        {
            return (false, "Verify your Google email, then try again.", null);
        }

        var email = (emailRaw ?? string.Empty).Trim();
        if (email.Length == 0 || !email.Contains('@'))
        {
            return (false, "Google did not return an email address.", null);
        }

        var sub = (subject ?? string.Empty).Trim();
        if (sub.Length == 0)
        {
            return (false, "Google sign-in could not be verified. Refresh and try again.", null);
        }

        return (true, null, new GoogleProfile(sub, email, givenName, familyName, name));
    }

    private static string? NormalizeClientId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Trim('\'').Trim('"').Trim();
    }

    private static string? TryReadAudience(string idToken)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(idToken);
            if (jwt.Audiences.Any())
            {
                return string.Join(',', jwt.Audiences);
            }

            return jwt.Payload.TryGetValue("aud", out var aud) ? aud?.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class GoogleTokenInfo
    {
        [JsonPropertyName("aud")]
        public string? Aud { get; set; }

        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("email_verified")]
        public string? EmailVerified { get; set; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}

public record GoogleProfile(string Subject, string Email, string? GivenName, string? FamilyName, string? Name);
