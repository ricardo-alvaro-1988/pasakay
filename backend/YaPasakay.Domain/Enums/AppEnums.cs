namespace YaPasakay.Domain.Enums;

public enum UserRole
{
    Admin = 1,
    Operator = 2,
    Rider = 3,
    Customer = 4
}

public enum VehicleType
{
    Motorcycle = 1,
    Tricycle = 2
}

public enum TripStatus
{
    Completed = 1,
    Cancelled = 2,
    Ongoing = 3,
    Pending = 4,
    Waiting = 5
}

public enum ChatSender
{
    Customer = 1,
    Rider = 2
}

public enum Gender
{
    Male = 1,
    Female = 2,
    Other = 3
}

public enum DeleteAccountStatus
{
    None = 0,
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum BillStatus
{
    Issued = 1
}

public enum NotificationKind
{
    Billing = 1,
    Announcement = 2,
    Sos = 3,
    AccountDelete = 4
}

public enum SurchargeKind
{
    TimeWindow = 1,
    DateRange = 2
}

public enum SupportKind
{
    Support = 1,
    Sos = 2
}

public enum SupportStatus
{
    Open = 1,
    Closed = 2
}

public enum SupportOpenedBy
{
    Customer = 1,
    Rider = 2
}

public enum AuditAction
{
    OperatorCreated = 1,
    OperatorUpdated = 2,
    OperatorActivated = 3,
    OperatorDeactivated = 4,
    BillIssued = 5
}

public enum PaymentMethod
{
    Cash = 1,
    GCash = 2,
    Maya = 3,
    Other = 4
}

public enum WalletTransactionKind
{
    CashIn = 1,
    CashOut = 2,
    Commission = 3
}

public enum WalletTransactionStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum OfferStatus
{
    Offered = 1,
    Accepted = 2,
    Expired = 3,
    Declined = 4
}
