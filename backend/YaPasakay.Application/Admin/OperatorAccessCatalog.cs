namespace YaPasakay.Application.Admin;

public static class OperatorAccessCatalog
{
    public static readonly IReadOnlyList<AccessPageItem> Pages =
    [
        new("dashboard", "Dashboard"),
        new("bookings", "Booking"),
        new("overview", "Overview"),
        new("schedule", "Schedule booking"),
        new("riders", "Riders"),
        new("customers", "Customers"),
        new("fleet", "Fleet"),
        new("fares", "Fare matrix"),
        new("derive-fares", "Derive fare"),
        new("surcharges", "Surcharges"),
        new("support", "Support"),
        new("inbox", "Inbox"),
        new("billing", "Billing"),
        new("wallet", "Wallet"),
        new("promos", "Promos"),
        new("commission", "Commission"),
        new("company", "Company"),
    ];

    public static readonly HashSet<string> PageIds = Pages.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> AllPageIds { get; } = Pages.Select(x => x.Id).ToList();

    public static bool IsKnown(string pageId) => PageIds.Contains(pageId);
}
