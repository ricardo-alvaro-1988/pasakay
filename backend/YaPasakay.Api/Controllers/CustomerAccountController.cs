using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Api.Services;
using YaPasakay.Application.Admin;
using YaPasakay.Application.Common;
using YaPasakay.Domain.Enums;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Controllers;

[ApiController]
[Authorize(Roles = "Customer")]
[Route("api/customer/account")]
public class CustomerAccountController(AppDbContext db) : ControllerBase
{
    [HttpPut("profile")]
    public async Task<ActionResult<CustomerDeskResponse>> UpdateProfile(
        [FromBody] CustomerProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var first = (request.FirstName ?? string.Empty).Trim();
        var last = (request.LastName ?? string.Empty).Trim();
        var email = (request.Email ?? string.Empty).Trim();
        if (first.Length == 0 || last.Length == 0)
        {
            return BadRequest(new { message = "Enter your first and last name." });
        }

        if (email.Length == 0 || !email.Contains('@'))
        {
            return BadRequest(new { message = "Enter a valid email address." });
        }

        if (!Enum.IsDefined(request.Gender))
        {
            return BadRequest(new { message = "Choose a gender." });
        }

        if (await db.Users.AnyAsync(x => x.Email == email && x.Id != customer.AppUserId, cancellationToken))
        {
            return BadRequest(new { message = "That email already has an account." });
        }

        customer.FirstName = first;
        customer.LastName = last;
        customer.Gender = request.Gender;
        customer.AppUser.FullName = $"{first} {last}";
        customer.AppUser.Email = email;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ReloadDeskAsync(customer.Id, cancellationToken));
    }

    [HttpPost("pin")]
    public async Task<ActionResult<CustomerDeskResponse>> SetPin(
        [FromBody] CustomerPinRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        var pin = (request.Pin ?? string.Empty).Trim();
        if (!SecretHasher.IsPin(pin))
        {
            return BadRequest(new { message = "PIN must be 4 to 6 digits." });
        }

        if (!string.IsNullOrWhiteSpace(customer.PinHash))
        {
            if (!SecretHasher.Verify(request.CurrentPin ?? string.Empty, customer.PinHash))
            {
                return BadRequest(new { message = "Current PIN is incorrect." });
            }
        }

        customer.PinHash = SecretHasher.Hash(pin);
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ReloadDeskAsync(customer.Id, cancellationToken));
    }

    [HttpPost("password")]
    public async Task<ActionResult<CustomerDeskResponse>> ChangePassword(
        [FromBody] CustomerPasswordChangeRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (!SecretHasher.Verify(request.CurrentPassword ?? string.Empty, customer.AppUser.PasswordHash))
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        if (!SecretHasher.IsStrongPassword(request.NewPassword))
        {
            return BadRequest(new { message = "New password must be at least 6 characters." });
        }

        customer.AppUser.PasswordHash = SecretHasher.Hash(request.NewPassword.Trim());
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ReloadDeskAsync(customer.Id, cancellationToken));
    }

    [HttpPut("mobile")]
    public async Task<ActionResult<CustomerDeskResponse>> UpdateMobile(
        [FromBody] CustomerMobileRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (!PhoneNormalizer.TryNormalizePhMobile(request.NewPhone, out var phone, out var phoneError))
        {
            return BadRequest(new { message = phoneError });
        }

        if (phone == customer.AppUser.PhoneNumber)
        {
            return BadRequest(new { message = "That is already your mobile number." });
        }

        if (await db.Users.AnyAsync(x => x.PhoneNumber == phone && x.Id != customer.AppUserId, cancellationToken))
        {
            return BadRequest(new { message = "That phone number already has an account." });
        }

        customer.AppUser.PhoneNumber = phone;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await ReloadDeskAsync(customer.Id, cancellationToken));
    }

    [HttpPost("delete")]
    public async Task<ActionResult<CustomerDeskResponse>> RequestDelete(
        [FromBody] CustomerDeleteRequest request,
        CancellationToken cancellationToken)
    {
        var (customer, status, message) = await CustomerContext.RequireAsync(db, User, cancellationToken);
        if (customer is null)
        {
            return StatusCode(status, new { message });
        }

        if (!string.IsNullOrWhiteSpace(customer.AppUser.GoogleSubject))
        {
            if (!string.IsNullOrWhiteSpace(customer.PinHash)
                && !SecretHasher.Verify(request.Pin ?? string.Empty, customer.PinHash))
            {
                return BadRequest(new { message = "PIN is incorrect." });
            }
        }
        else if (!SecretHasher.Verify(request.Password ?? string.Empty, customer.AppUser.PasswordHash))
        {
            return BadRequest(new { message = "Password is incorrect." });
        }

        var reason = (request.Reason ?? string.Empty).Trim();
        if (reason.Length < 4)
        {
            return BadRequest(new { message = "Tell us why you want to delete this account." });
        }

        if (customer.DeleteStatus == DeleteAccountStatus.Pending)
        {
            return BadRequest(new { message = "Your deletion request is already pending." });
        }

        customer.DeleteStatus = DeleteAccountStatus.Pending;
        customer.DeleteRequestedAtUtc = DateTime.UtcNow;
        customer.DeleteRequestReason = reason;
        customer.DeleteResolvedAtUtc = null;
        customer.DeleteResolutionNote = null;
        customer.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return Ok(await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken));
    }

    private async Task<CustomerDeskResponse> ReloadDeskAsync(Guid customerId, CancellationToken cancellationToken)
    {
        var customer = await db.CustomerProfiles
            .Include(x => x.AppUser)
            .FirstAsync(x => x.Id == customerId, cancellationToken);
        return await CustomerDeskBuilder.BuildAsync(db, customer, cancellationToken);
    }
}
