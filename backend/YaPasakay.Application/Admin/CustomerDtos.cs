using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public record CustomerPlaceItem(
    Guid BarangayId,
    string Label,
    string Details,
    string Barangay,
    string Municipality,
    double Lat,
    double Lng);

public record CustomerBookRequest(
    VehicleType VehicleType,
    Guid? PickupBarangayId,
    string PickupDetails,
    double? PickupLat,
    double? PickupLng,
    Guid? DropoffBarangayId,
    string DropoffDetails,
    double? DropoffLat,
    double? DropoffLng,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther,
    string? Notes,
    DateTime? ScheduledAtUtc,
    Guid? RiderId,
    bool HailQr);

public record CustomerHailRider(
    Guid RiderId,
    string FullName,
    string PlateNumber,
    VehicleType VehicleType,
    string? VehicleModel,
    string? PhotoUrl,
    string? PhoneNumber,
    bool IsOnline,
    bool IsBusy,
    string CompanyName,
    IReadOnlyList<PaymentMethod> PaymentMethods);

public record TripChatSendRequest(string Body);

public record CustomerQuoteResponse(
    decimal Fare,
    decimal DistanceKm,
    int EtaMinutes,
    string OperatorName,
    VehicleType VehicleType,
    PaymentMethod PaymentMethod,
    bool RiderAvailable);

public record CustomerServiceCheckRequest(
    Guid? PickupBarangayId,
    string PickupDetails,
    double? PickupLat,
    double? PickupLng,
    Guid? DropoffBarangayId,
    string DropoffDetails);

public record CustomerServiceCheckResponse(
    bool MunicipalityHasOperator,
    string? MunicipalityName);

public record CustomerTripItem(
    Guid Id,
    string Reference,
    TripStatus Status,
    string Pickup,
    string Dropoff,
    double? PickupLat,
    double? PickupLng,
    double? DropoffLat,
    double? DropoffLng,
    decimal Fare,
    decimal DistanceKm,
    VehicleType VehicleType,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther,
    string OperatorName,
    string? RiderName,
    string? RiderPhone,
    string? PlateNumber,
    string? VehicleModel,
    string? RiderPhotoUrl,
    double? RiderLat,
    double? RiderLng,
    DateTime RequestedAtUtc,
    DateTime? ScheduledAtUtc,
    bool CanCancel,
    bool CanSos,
    bool HailQr,
    int? Rating,
    string? RatingComment,
    bool CanRate,
    bool CanViewChat,
    bool CanChat);

public record CustomerRateRequest(int Rating, string? Comment);

public record CustomerDeskResponse(
    Guid CustomerId,
    string FullName,
    string FirstName,
    string LastName,
    string PhoneNumber,
    string? Email,
    Gender? Gender,
    bool HasPin,
    DeleteAccountStatus DeleteStatus,
    CustomerTripItem? ActiveTrip,
    IReadOnlyList<CustomerTripItem> Scheduled,
    IReadOnlyList<CustomerTripItem> Recent,
    IReadOnlyList<CustomerPlaceItem> Places,
    double? MapLat,
    double? MapLng,
    CustomerHailRider? HailedRider,
    CustomerTripItem? PendingRating,
    bool NeedsMobile);

public record CustomerProfileUpdateRequest(
    string FirstName,
    string LastName,
    Gender Gender,
    string Email);

public record CustomerPinRequest(string Pin, string? CurrentPin);
public record CustomerPasswordChangeRequest(string CurrentPassword, string NewPassword);
public record CustomerMobileRequest(string NewPhone);
public record CustomerDeleteRequest(string Reason, string? Password = null, string? Pin = null);
