using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Auth;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    IOtpStore otpStore,
    IOtpSender otpSender,
    GoogleTokenValidator google,
    ITokenService tokens) : ControllerBase
{
    [HttpPost("request-otp")]
    public async Task<IActionResult> RequestOtp([FromBody] RequestOtpRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNormalizer.Normalize(request.Phone);
        if (phone.Length < 10)
        {
            return BadRequest(new { message = "Enter a valid phone number." });
        }

        var existing = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (existing?.Role is UserRole.Rider or UserRole.Operator or UserRole.Admin)
        {
            return BadRequest(new { message = existing.Role == UserRole.Admin
                ? "Admins sign in with phone and password."
                : existing.Role == UserRole.Rider
                ? "Riders sign in with phone and password."
                : "Operators sign in with phone and password." });
        }

        otpStore.Save(phone, FixedOtpSender.DevCode, TimeSpan.FromMinutes(10));
        await otpSender.SendAsync(phone, FixedOtpSender.DevCode, cancellationToken);
        return Ok(new { message = "OTP sent." });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] PasswordLoginRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNormalizer.Normalize(request.Phone);
        var password = (request.Password ?? string.Empty).Trim();
        if (phone.Length < 10 || password.Length == 0)
        {
            return BadRequest(new { message = "Enter your phone and password." });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "No account for this number." });
        }

        if (user.Role is not UserRole.Rider and not UserRole.Operator and not UserRole.Admin)
        {
            return Unauthorized(new { message = "Customers sign in with Google." });
        }

        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return Unauthorized(new { message = user.Role == UserRole.Admin
                ? "Ask Super Admin to set a password, or set one from Profile after OTP is retired."
                : user.Role == UserRole.Rider
                ? "Ask your operator to set a password."
                : "Ask Super Admin to set a password." });
        }

        if (!SecretHasher.Verify(password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Wrong phone or password." });
        }

        var blocked = await GateAsync(user, cancellationToken);
        if (blocked is not null)
        {
            return blocked;
        }

        var me = await AdminAccess.ToMeAsync(db, user, cancellationToken);
        if (user.Role == UserRole.Admin && !user.IsMainAdmin && me.AccessPages.Count == 0)
        {
            return Unauthorized(new { message = "This account has no admin pages. Ask Super Admin to assign a role." });
        }

        return Ok(await IssueAsync(user, me, cancellationToken));
    }

    [HttpPost("verify-otp")]
    public async Task<ActionResult<AuthResponse>> VerifyOtp([FromBody] VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        var phone = PhoneNormalizer.Normalize(request.Phone);
        var code = (request.Code ?? string.Empty).Trim();
        if (code.Length == 0)
        {
            code = FixedOtpSender.DevCode;
        }

        if (!otpStore.TryValidate(phone, code))
        {
            return Unauthorized(new { message = "Invalid or expired code." });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.PhoneNumber == phone, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { message = "No account for this number." });
        }

        if (user.Role == UserRole.Customer)
        {
            return Unauthorized(new { message = "Customers sign in with Google." });
        }

        if (user.Role is UserRole.Rider or UserRole.Operator or UserRole.Admin)
        {
            return Unauthorized(new { message = user.Role == UserRole.Admin
                ? "Admins sign in with phone and password."
                : user.Role == UserRole.Rider
                ? "Riders sign in with phone and password."
                : "Operators sign in with phone and password." });
        }

        var blocked = await GateAsync(user, cancellationToken);
        if (blocked is not null)
        {
            return blocked;
        }

        var me = await AdminAccess.ToMeAsync(db, user, cancellationToken);
        if (user.Role == UserRole.Admin && me.AccessPages.Count == 0)
        {
            return Unauthorized(new { message = "This account has no admin pages. Ask the main admin to assign a user group." });
        }

        return Ok(await IssueAsync(user, me, cancellationToken));
    }

    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> Google([FromBody] GoogleSignInRequest request, CancellationToken cancellationToken)
    {
        var (ok, error, profile) = await google.ValidateAsync(request.IdToken);
        if (!ok || profile is null)
        {
            return BadRequest(new { message = error });
        }

        var user = await db.Users.FirstOrDefaultAsync(x => x.GoogleSubject == profile.Subject, cancellationToken);
        if (user is null)
        {
            user = await db.Users.FirstOrDefaultAsync(
                x => x.Email != null && x.Email.ToLower() == profile.Email.ToLower(),
                cancellationToken);
        }

        if (user is not null && user.Role != UserRole.Customer)
        {
            return Unauthorized(new { message = "This Google account is not a customer login." });
        }

        if (user is not null && !user.IsActive)
        {
            return Unauthorized(new { message = "Account not found or inactive." });
        }

        if (user is null)
        {
            var (first, last) = SplitName(profile.GivenName, profile.FamilyName, profile.Name);
            user = new AppUser
            {
                PhoneNumber = PlaceholderPhone(profile.Subject),
                FullName = $"{first} {last}".Trim(),
                Email = profile.Email,
                GoogleSubject = profile.Subject,
                Role = UserRole.Customer,
                IsActive = true
            };
            db.Users.Add(user);
            db.CustomerProfiles.Add(new CustomerProfile
            {
                AppUser = user,
                FirstName = first,
                LastName = last,
                Gender = Gender.Other
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            user.GoogleSubject ??= profile.Subject;
            user.Email = profile.Email;
            if (string.IsNullOrWhiteSpace(user.FullName))
            {
                user.FullName = profile.Name?.Trim() ?? user.FullName;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        var blocked = await GateAsync(user, cancellationToken);
        if (blocked is not null)
        {
            return blocked;
        }

        var me = await AdminAccess.ToMeAsync(db, user, cancellationToken);
        return Ok(await IssueAsync(user, me, cancellationToken));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        var stored = await db.RefreshTokens
            .Include(x => x.AppUser)
            .FirstOrDefaultAsync(x => x.Token == request.RefreshToken, cancellationToken);

        if (stored is null || !stored.IsActive || !stored.AppUser.IsActive)
        {
            return Unauthorized(new { message = "Refresh token is invalid." });
        }

        stored.RevokedAtUtc = DateTime.UtcNow;
        var blocked = await GateAsync(stored.AppUser, cancellationToken);
        if (blocked is not null)
        {
            return blocked;
        }

        var me = await AdminAccess.ToMeAsync(db, stored.AppUser, cancellationToken);
        if (stored.AppUser.Role == UserRole.Admin && me.AccessPages.Count == 0)
        {
            return Unauthorized(new { message = "This account has no admin pages. Ask the main admin to assign a user group." });
        }

        return Ok(await IssueAsync(stored.AppUser, me, cancellationToken));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(await AdminAccess.ToMeAsync(db, user, cancellationToken));
    }

    private async Task<ActionResult?> GateAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (user.Role == UserRole.Operator && user.OperatorId is Guid operatorId)
        {
            var active = await db.Operators.AnyAsync(x => x.Id == operatorId && x.IsActive, cancellationToken);
            if (!active)
            {
                return Unauthorized(new { message = "This Operator is inactive. Ask Super Admin to activate the company." });
            }
        }

        return null;
    }

    private async Task<AppUser?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var id = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var userId)
            ? await db.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            : null;
    }

    private async Task<AuthResponse> IssueAsync(AppUser user, MeResponse me, CancellationToken cancellationToken)
    {
        var (access, expires) = tokens.CreateAccessToken(user);
        var refresh = tokens.CreateRefreshToken();
        db.RefreshTokens.Add(new RefreshToken
        {
            AppUserId = user.Id,
            Token = refresh,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync(cancellationToken);
        return new AuthResponse(access, refresh, expires, me);
    }

    private static (string First, string Last) SplitName(string? given, string? family, string? full)
    {
        var first = (given ?? string.Empty).Trim();
        var last = (family ?? string.Empty).Trim();
        if (first.Length > 0)
        {
            return (first, last.Length > 0 ? last : "Customer");
        }

        var parts = (full ?? "Google Customer").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? (parts[0], parts[1]) : (parts[0], "Customer");
    }

    private static string PlaceholderPhone(string subject)
    {
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(subject)))
            .ToLowerInvariant();
        return "g" + hash[..19];
    }
}
