using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class GoogleTokenValidator(IOptions<GoogleAuthOptions> options)
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

        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(
                idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });

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
        catch (InvalidJwtException)
        {
            return (false, "Google sign-in could not be verified. Refresh and try again.", null);
        }
        catch
        {
            return (false, "Google sign-in could not be verified. Refresh and try again.", null);
        }
    }
}

public record GoogleProfile(string Subject, string Email, string? GivenName, string? FamilyName, string? Name);
