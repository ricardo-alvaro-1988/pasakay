using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public record OverviewSeriesPoint(
    DateOnly Date,
    int OperatorsCreated,
    int CustomersRegistered,
    int TripsCompleted);

public record OverviewResponse(
    int Operators,
    int Riders,
    int RidersMotorcycle,
    int RidersTricycle,
    int Customers,
    int TripsToday,
    decimal AdminCutToday,
    int OpenSos,
    int UnreadSosAlerts,
    int PendingAccountDeletes,
    IReadOnlyList<OverviewSeriesPoint> Series,
    IReadOnlyList<OperatorListItem> RecentOperators);

public record OperatorListItem(
    Guid Id,
    string CompanyName,
    string ContactName,
    string ContactPhone,
    string FullAddress,
    string AreaOfOperation,
    string GovernmentIdType,
    string GovernmentId,
    string? ProfilePhotoUrl,
    string? GovernmentIdPhotoUrl,
    bool IsActive,
    decimal MotorcycleCommissionPercent,
    decimal TricycleCommissionPercent,
    int RiderCount,
    int RidersMotorcycle,
    int RidersTricycle,
    DateTime CreatedAtUtc);

public record SetActiveRequest(bool IsActive);

public record OperatorAreaItem(
    Guid BarangayId,
    string Barangay,
    string Municipality,
    string Province);

public record IdName(Guid Id, string Name);

public record BarangayOption(
    Guid Id,
    string Name,
    Guid MunicipalityId,
    string Municipality,
    Guid ProvinceId,
    string Province);

public record OperatorAddressItem(
    Guid? BarangayId,
    Guid? MunicipalityId,
    Guid? ProvinceId,
    string Barangay,
    string Municipality,
    string Province,
    string Details,
    string FullAddress);

public record OperatorDetailResponse(
    Guid Id,
    string CompanyName,
    string ContactName,
    string ContactPhone,
    string FullAddress,
    string AreaOfOperation,
    string GovernmentIdType,
    string GovernmentId,
    string? ProfilePhotoUrl,
    string? GovernmentIdPhotoUrl,
    bool IsActive,
    decimal MotorcycleCommissionPercent,
    decimal TricycleCommissionPercent,
    int RiderCount,
    int RidersMotorcycle,
    int RidersTricycle,
    DateTime CreatedAtUtc,
    OperatorAddressItem Address,
    IReadOnlyList<OperatorAreaItem> Areas,
    IReadOnlyList<RiderListItem> Riders);

public record RiderListItem(
    Guid Id,
    string FullName,
    string PhoneNumber,
    VehicleType VehicleType,
    string PlateNumber,
    string? VehicleModel,
    bool IsActive,
    string LicenseType,
    string LicenseNumber,
    string? ProfilePhotoUrl,
    string? LicensePhotoUrl,
    IReadOnlyList<PaymentMethod> AcceptedPaymentMethods);

public record RiderDetailResponse(
    Guid Id,
    string FullName,
    string PhoneNumber,
    VehicleType VehicleType,
    string PlateNumber,
    string? VehicleModel,
    bool IsActive,
    string LicenseType,
    string LicenseNumber,
    string? ProfilePhotoUrl,
    string? LicensePhotoUrl,
    string FullAddress,
    OperatorAddressItem Address,
    IReadOnlyList<PaymentMethod> AcceptedPaymentMethods);

public record RideStopItem(
    string Details,
    string Barangay,
    string Municipality,
    string Province,
    string FullAddress);

public record RideListItem(
    Guid Id,
    string Reference,
    DateTime RequestedAtUtc,
    string Pickup,
    string Dropoff,
    string CustomerName,
    VehicleType VehicleType,
    TripStatus Status,
    decimal Fare,
    decimal DistanceKm,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther);

public record RideDetailResponse(
    Guid Id,
    string Reference,
    TripStatus Status,
    string CustomerName,
    string CustomerPhone,
    RideStopItem PickupStop,
    RideStopItem DropoffStop,
    string Pickup,
    string Dropoff,
    string? Notes,
    decimal Fare,
    decimal DistanceKm,
    int? DurationMinutes,
    VehicleType VehicleType,
    DateTime RequestedAtUtc,
    DateTime? ScheduledAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? CancelledAtUtc,
    string? CancelReason,
    int? Rating,
    string? RatingComment,
    DateTime? RatedAtUtc,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther,
    Guid OperatorId,
    string OperatorName,
    string OperatorPhone,
    Guid RiderId,
    string RiderName,
    string RiderPhone,
    string PlateNumber,
    string? VehicleModel,
    string? RiderPhotoUrl,
    IReadOnlyList<RideChatMessageItem> Chat);

public record RideChatMessageItem(
    Guid Id,
    ChatSender Sender,
    string Body,
    DateTime SentAtUtc,
    string? PhotoUrl);

public record RideSeriesPoint(DateOnly Date, int Completed);

public record RiderRideSummary(
    int Total,
    int Completed,
    int Cancelled,
    int Ongoing,
    decimal GrossFare);

public record RiderRidesResponse(
    RiderRideSummary Summary,
    IReadOnlyList<RideSeriesPoint> Series,
    PagedResult<RideListItem> Rides);

public record CustomerListItem(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string PhoneNumber,
    DateTime RegisteredAtUtc,
    bool IsActive,
    string? PhotoUrl,
    DeleteAccountStatus DeleteStatus);

public record CustomerDeleteRequestItem(
    DeleteAccountStatus Status,
    DateTime? RequestedAtUtc,
    string? Reason,
    DateTime? ResolvedAtUtc,
    string? ResolutionNote);

public record CustomerDetailResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string PhoneNumber,
    DateTime RegisteredAtUtc,
    bool IsActive,
    string? PhotoUrl,
    CustomerDeleteRequestItem DeleteRequest);

public record RecordDeleteRequest(string? Reason);

public record ResolveDeleteRequest(bool Approve, string? Note);

public record SearchHit(string Kind, Guid Id, string Name, string Phone, string? PhotoUrl);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

public record TerritoryListItem(
    Guid Id,
    Guid ProvinceId,
    string Province,
    string Municipality,
    IReadOnlyList<string> Barangays,
    int BarangayCount,
    int OperatorCount);

public record FareSampleItem(decimal DistanceKm, decimal Fare);

public record FareSurchargeItem(
    Guid Id,
    SurchargeKind Kind,
    string Name,
    decimal Amount,
    string? WindowStart,
    string? WindowEnd,
    DateTime? RangeStartUtc,
    DateTime? RangeEndUtc,
    bool IsActive);

public record FareRatesItem(
    VehicleType VehicleType,
    decimal BaseFare,
    decimal PerKm,
    decimal MinimumFare,
    decimal IncludedKm,
    decimal OperatorCommissionPercent,
    decimal DriverCommissionPercent,
    bool IsActive,
    IReadOnlyList<FareSurchargeItem> Surcharges,
    IReadOnlyList<FareSampleItem> Samples);

public record OperatorFareListItem(
    Guid OperatorId,
    string OperatorName,
    bool OperatorActive,
    decimal MotorcycleCommissionPercent,
    decimal TricycleCommissionPercent,
    FareRatesItem? Motorcycle,
    FareRatesItem? Tricycle);

public record OperatorFareDetailResponse(
    Guid OperatorId,
    string OperatorName,
    bool OperatorActive,
    decimal MotorcycleCommissionPercent,
    decimal TricycleCommissionPercent,
    FareRatesItem? Motorcycle,
    FareRatesItem? Tricycle);

public record BillingOperatorListItem(
    Guid OperatorId,
    string CompanyName,
    string ContactName,
    string ContactPhone,
    string? ProfilePhotoUrl,
    bool IsActive,
    decimal MotorcycleCommissionPercent,
    decimal TricycleCommissionPercent,
    decimal PendingCommission,
    decimal PendingMotorcycle,
    decimal PendingTricycle,
    int PendingTripCount,
    DateTime? OldestUnbilledUtc,
    DateTime? NewestUnbilledUtc);

public record BillTripItem(
    DateTime AtUtc,
    string RiderName,
    string BookingNumber,
    decimal Fare,
    decimal Amount);

public record BillListItem(
    Guid Id,
    string Number,
    BillStatus Status,
    decimal Amount,
    decimal MotorcycleAmount,
    decimal TricycleAmount,
    int TripCount,
    DateTime PeriodFromUtc,
    DateTime PeriodToUtc,
    bool DisabledOperator,
    DateTime NotifiedAtUtc,
    DateTime CreatedAtUtc,
    string? Note,
    IReadOnlyList<BillTripItem> Trips);

public record BillingOperatorDetail(
    Guid OperatorId,
    string CompanyName,
    string ContactName,
    string ContactPhone,
    string? ProfilePhotoUrl,
    bool IsActive,
    int RiderCount,
    decimal MotorcycleCommissionPercent,
    decimal TricycleCommissionPercent,
    decimal PendingCommission,
    decimal PendingMotorcycle,
    decimal PendingTricycle,
    int PendingTripCount,
    DateTime? OldestUnbilledUtc,
    DateTime? NewestUnbilledUtc,
    IReadOnlyList<BillListItem> Bills);

public record CreateBillRequest(bool DisableOperator, string? Note);

public record AnnouncementListItem(
    Guid Id,
    string Title,
    string Body,
    bool ForOperators,
    bool ForRiders,
    bool ForCustomers,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive,
    DateTime CreatedAtUtc);

public record CreateAnnouncementRequest(
    string Title,
    string Body,
    bool ForOperators,
    bool ForRiders,
    bool ForCustomers,
    DateTime? StartsAtUtc,
    DateTime? EndsAtUtc);

public record SupportTicketItem(
    Guid Id,
    SupportKind Kind,
    SupportStatus Status,
    SupportOpenedBy OpenedBy,
    string OpenedByName,
    string OpenedByPhone,
    string Subject,
    string Body,
    string? OperatorNotes,
    Guid OperatorId,
    string OperatorName,
    string OperatorPhone,
    string Municipality,
    Guid? TripId,
    string? BookingNumber,
    DateTime CreatedAtUtc,
    DateTime? ClosedAtUtc);

public record SupportInboxResult(
    IReadOnlyList<SupportTicketItem> Items,
    int Page,
    int PageSize,
    int Total,
    int OpenSos,
    int OpenTickets,
    int ClosedTickets,
    int UnreadSosAlerts);

public record AdminAlertsSummary(
    int OpenSos,
    int UnreadSosAlerts,
    int PendingBilling,
    int PendingAccountDeletes);

public record OperatorNavAlertsResponse(
    int PendingWalletRequests,
    int OpenSos,
    int UnreadBilling,
    int PendingAccountDeletes);

public record AdminAlertItem(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Body,
    Guid? SupportTicketId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public record CreateSosRequest(
    Guid TripId,
    string? Message,
    double? Lat,
    double? Lng);

public record MapPointItem(
    double Lat,
    double Lng,
    string Label,
    DateTime? AtUtc);

public record SupportTicketDetailResponse(
    SupportTicketItem Ticket,
    RideDetailResponse? Booking,
    MapPointItem? SosLocation,
    MapPointItem? RiderLocation,
    MapPointItem? PickupLocation,
    MapPointItem? DropoffLocation);

public record AuditLogItem(
    Guid Id,
    AuditAction Action,
    string ActionLabel,
    string Summary,
    Guid OperatorId,
    string OperatorName,
    Guid? ActorUserId,
    string ActorName,
    DateTime CreatedAtUtc);

public record AccessGroupItem(
    Guid Id,
    string Name,
    string Description,
    int UserCount,
    IReadOnlyList<string> Pages);

public record SaveAccessGroupRequest(
    string Name,
    string? Description,
    IReadOnlyList<string> Pages);

public record AccessStaffItem(
    Guid Id,
    string FullName,
    string PhoneNumber,
    Guid AccessGroupId,
    string AccessGroupName,
    bool IsActive,
    bool IsMainAdmin,
    DateTime CreatedAtUtc);

public record SaveAccessStaffRequest(
    string FullName,
    string Phone,
    Guid AccessGroupId,
    string? Password);

public record ResetPasswordResult(
    string PhoneNumber,
    string Otp,
    string Message);

public record SetPasswordRequest(string Password);

public record FleetRiderItem(
    Guid Id,
    string FullName,
    string PhoneNumber,
    VehicleType VehicleType,
    string PlateNumber,
    string? ProfilePhotoUrl,
    double Lat,
    double Lng,
    DateTime LastLocationAtUtc,
    bool IsOnline,
    TripStatus? Status,
    string? BookingReference);

public record OperatorFleetResponse(
    int Active,
    int OnMap,
    int Motorcycle,
    int Tricycle,
    IReadOnlyList<FleetRiderItem> Riders);

public record ScheduledBookingItem(
    Guid Id,
    string Reference,
    DateTime ScheduledAtUtc,
    string CustomerName,
    string CustomerPhone,
    Guid RiderId,
    string RiderName,
    string PlateNumber,
    VehicleType VehicleType,
    string Pickup,
    string Dropoff,
    TripStatus Status,
    decimal Fare,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther);

public record CreateScheduledBookingRequest(
    string CustomerName,
    string Phone,
    Guid RiderId,
    Guid PickupBarangayId,
    string PickupDetails,
    Guid DropoffBarangayId,
    string DropoffDetails,
    DateTime ScheduledAtUtc,
    string? Notes,
    decimal DistanceKm,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther);

public record ReassignBookingRequest(Guid RiderId);

public record OperatorBookingListItem(
    Guid Id,
    string Reference,
    DateTime RequestedAtUtc,
    DateTime? ScheduledAtUtc,
    string CustomerName,
    string CustomerPhone,
    string RiderName,
    string PlateNumber,
    VehicleType VehicleType,
    string Pickup,
    string Dropoff,
    TripStatus Status,
    decimal Fare,
    PaymentMethod PaymentMethod,
    string? PaymentMethodOther);

public record OperatorBookingColumn(
    int Total,
    IReadOnlyList<RideListItem> Items);

public record OperatorBookingBoardResponse(
    OperatorBookingColumn Pending,
    OperatorBookingColumn Waiting,
    OperatorBookingColumn Ongoing,
    OperatorBookingColumn Completed);

public record OperatorOverviewSeriesPoint(
    DateOnly Date,
    decimal Sales,
    int Pending,
    int Ongoing,
    int Complete);

public record OperatorOverviewResponse(
    string CompanyName,
    bool IsActive,
    int Riders,
    int RidersMotorcycle,
    int RidersTricycle,
    int TripsToday,
    int OpenSos,
    int OpenTickets,
    decimal PendingCommission,
    int UnreadInbox,
    decimal SalesToday,
    int PendingNow,
    int OngoingNow,
    int CompleteToday,
    IReadOnlyList<OperatorOverviewSeriesPoint> Series);

public record OperatorInboxItem(
    Guid Id,
    NotificationKind Kind,
    string Title,
    string Body,
    Guid? BillId,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public record SaveFareRatesRequest(
    VehicleType VehicleType,
    decimal BaseFare,
    decimal PerKm,
    decimal MinimumFare,
    decimal IncludedKm,
    decimal OperatorCommissionPercent,
    decimal DriverCommissionPercent,
    bool IsActive);

public record FareVehicleRatesBody(
    decimal BaseFare,
    decimal PerKm,
    decimal MinimumFare,
    decimal IncludedKm,
    decimal OperatorCommissionPercent,
    decimal DriverCommissionPercent,
    bool IsActive);

public record SaveRelatedFareRatesRequest(
    FareVehicleRatesBody Motorcycle,
    FareVehicleRatesBody Tricycle);

public record SaveFareSurchargeRequest(
    SurchargeKind Kind,
    string Name,
    decimal Amount,
    string? WindowStart,
    string? WindowEnd,
    DateTime? RangeStartUtc,
    DateTime? RangeEndUtc,
    bool IsActive);

public record SaveRelatedFareSurchargeRequest(
    IReadOnlyList<VehicleType>? VehicleTypes,
    SurchargeKind Kind,
    string Name,
    decimal Amount,
    string? WindowStart,
    string? WindowEnd,
    DateTime? RangeStartUtc,
    DateTime? RangeEndUtc,
    bool IsActive);

public record SupportNoteRequest(string Notes);

public record CloseTicketRequest(bool Closed);

public record WalletTransactionItem(
    Guid Id,
    WalletTransactionKind Kind,
    WalletTransactionStatus Status,
    PaymentMethod? PaymentMethod,
    decimal Amount,
    decimal? BalanceAfter,
    Guid? TripId,
    string? TripReference,
    decimal? TripFare,
    string? Note,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);

public record WalletHistoryItem(
    Guid Id,
    Guid RiderId,
    string RiderName,
    string RiderPhone,
    string PlateNumber,
    WalletTransactionKind Kind,
    WalletTransactionStatus Status,
    PaymentMethod? PaymentMethod,
    decimal Amount,
    decimal? BalanceAfter,
    Guid? TripId,
    string? TripReference,
    string? Note,
    string? RejectionReason,
    DateTime CreatedAtUtc,
    DateTime? ResolvedAtUtc);

public record RiderWalletResponse(
    Guid RiderId,
    string RiderName,
    decimal Balance,
    int PendingCount,
    IReadOnlyList<WalletTransactionItem> Recent);

public record RiderWalletDetailResponse(
    Guid RiderId,
    string RiderName,
    string RiderPhone,
    decimal Balance,
    int PendingCount,
    IReadOnlyList<WalletTransactionItem> Transactions);

public record WalletRequestItem(
    Guid Id,
    Guid RiderId,
    string RiderName,
    string RiderPhone,
    string PlateNumber,
    WalletTransactionKind Kind,
    PaymentMethod PaymentMethod,
    decimal Amount,
    string? Note,
    DateTime CreatedAtUtc);

public record RiderWalletBalanceItem(
    Guid RiderId,
    string RiderName,
    string RiderPhone,
    string PlateNumber,
    VehicleType VehicleType,
    bool IsActive,
    decimal Balance,
    int PendingCount);

public record OperatorWalletOverviewResponse(
    decimal TotalBalance,
    int PendingRequests,
    IReadOnlyList<RiderWalletBalanceItem> Riders);

public record CreateWalletRequestBody(
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Note);

public record CreateOperatorWalletRequestBody(
    decimal Amount,
    PaymentMethod PaymentMethod,
    string? Note,
    bool Approved);

public record RejectWalletRequestBody(string? Reason);

public record ResolveWalletRequestResult(
    WalletTransactionItem Transaction,
    decimal Balance);

public static class UploadUrls
{
    public static string? FromPath(string? relativePath) =>
        string.IsNullOrWhiteSpace(relativePath) ? null : "/uploads/" + relativePath.Replace('\\', '/');
}
