namespace YaPasakay.Application.Admin;

public record RiderInviteLinkResponse(string Token, string JoinPath, string StatusPath, DateTime CreatedAtUtc);

public record RiderInvitePublicInfo(
    string Token,
    string CompanyName,
    string StatusPath);

public record RiderApplicationListItem(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string VehicleType,
    string PlateNumber,
    string Status,
    DateTime CreatedAtUtc);

public record RiderApplicationDetailResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    string VehicleType,
    string PlateNumber,
    string? VehicleModel,
    string LicenseType,
    string LicenseNumber,
    string Status,
    string FullAddress,
    OperatorAddressItem Address,
    IReadOnlyList<string> AcceptedPaymentMethods,
    string? ProfilePhotoUrl,
    string? LicensePhotoUrl,
    string? ReviewNote,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    Guid? RiderProfileId);

public record RiderApplicationStatusResponse(
    string Status,
    string Label,
    string? CompanyName,
    string? Message);
