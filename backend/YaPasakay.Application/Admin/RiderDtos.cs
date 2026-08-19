using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public record RiderHailBody(Guid CustomerId);

public record RiderPendingHail(
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    DateTime ScannedAtUtc);

public record RiderLocationBody(double Lat, double Lng);

public record RiderOnlineBody(bool Online);

public record RiderPaymentsBody(IReadOnlyList<PaymentMethod> PaymentMethods);

public record RiderPasswordChangeRequest(string CurrentPassword, string NewPassword);

public record RiderOfferItem(
    Guid OfferId,
    Guid TripId,
    string Reference,
    TripStatus TripStatus,
    string CustomerName,
    string CustomerPhone,
    string Pickup,
    string Dropoff,
    double? PickupLat,
    double? PickupLng,
    double? DropoffLat,
    double? DropoffLng,
    decimal Fare,
    decimal DistanceKm,
    double? RiderDistanceKm,
    VehicleType VehicleType,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther,
    DateTime RequestedAtUtc,
    DateTime? ScheduledAtUtc,
    DateTime ExpiresAtUtc,
    bool IsPreferred,
    bool Highlighted);

public record RiderActiveTrip(
    Guid TripId,
    string Reference,
    TripStatus Status,
    string CustomerName,
    string CustomerPhone,
    int PreviousBookingCount,
    int CompletedBookingCount,
    int CancelledBookingCount,
    DateTime? LastCompletedAtUtc,
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
    DateTime RequestedAtUtc,
    DateTime? ScheduledAtUtc,
    bool CanStart,
    bool CanComplete,
    bool CanSos,
    bool CanViewChat,
    bool CanChat);

public record RiderDeskResponse(
    Guid RiderId,
    string FullName,
    string PhoneNumber,
    string PlateNumber,
    VehicleType VehicleType,
    string? PhotoUrl,
    string CompanyName,
    bool IsOnline,
    decimal WalletBalance,
    decimal MinWalletToReceive,
    bool CanReceiveBookings,
    bool WalletLow,
    string WalletHighlight,
    IReadOnlyList<PaymentMethod> PaymentMethods,
    RiderActiveTrip? ActiveTrip,
    IReadOnlyList<RiderOfferItem> Offers,
    RiderPendingHail? PendingHail,
    string? VehicleModel,
    string LicenseType,
    string LicenseNumber,
    string? LicensePhotoUrl,
    string FullAddress,
    bool IsActive);
