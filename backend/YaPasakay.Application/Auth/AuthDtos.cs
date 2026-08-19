using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Auth;

public record RequestOtpRequest(string Phone);
public record VerifyOtpRequest(string Phone, string Code);
public record PasswordLoginRequest(string Phone, string Password);
public record RefreshRequest(string RefreshToken);
public record GoogleSignInRequest(string IdToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    MeResponse User);

public record MeResponse(
    Guid Id,
    string PhoneNumber,
    string FullName,
    UserRole Role,
    Guid? OperatorId,
    bool IsActive,
    bool IsMainAdmin,
    string? AccessGroupName,
    string? CompanyName,
    IReadOnlyList<string> AccessPages);
