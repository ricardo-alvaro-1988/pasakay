using System.IdentityModel.Tokens.Jwt;
using Google.Apis.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class GoogleTokenValidator(
    IOptions<GoogleAuthOptions> options,
    ILogger<GoogleTokenValidator> logger)
{
    public async Task<(bool Ok, string? Error, GoogleProfile? Profile)> ValidateAsync(string? idToken)
    {
        var clientId = options.Value.ClientId?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return (false, "Google sign-in is not configured. Add GoogleAuth:ClientId to appsettings.", null);
        }

        if (string.IsNullOrWhiteSpace(idToken))
        {
            return (false, "Google sign-in was cancelled.", null);
        }

        var audiences = new List<string> { clientId };
        foreach (var extra in options.Value.AdditionalClientIds ?? [])
        {
            var value = extra?.Trim();
            if (!string.IsNullOrWhiteSpace(value) && !audiences.Contains(value, StringComparer.Ordinal))
            {
                audiences.Add(value);
            }
        }

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

            if (payload.EmailVerified != true)
            {
                return (false, "Verify your Google email, then try again.", null);
            }

            var email = (payload.Email ?? string.Empty).Trim();
            if (email.Length == 0 || !email.Contains('@'))
            {
                return (false, "Google did not return an email address.", null);
            }

            return (true, null, new GoogleProfile(
                payload.Subject,
                email,
                payload.GivenName,
                payload.FamilyName,
                payload.Name));
        }
        catch (InvalidJwtException ex)
        {
            var aud = TryReadAudience(idToken);
            logger.LogWarning(
                ex,
                "Google ID token rejected. configuredAudience={Configured} tokenAud={TokenAud}",
                string.Join(',', audiences),
                aud ?? "(unknown)");
            return (false, "Google sign-in could not be verified. Refresh and try again.", null);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Google ID token validation failed unexpectedly.");
            return (false, "Google sign-in could not be verified. Refresh and try again.", null);
        }
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
}

public record GoogleProfile(string Subject, string Email, string? GivenName, string? FamilyName, string? Name);
