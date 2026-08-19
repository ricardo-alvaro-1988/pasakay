namespace YaPasakay.Application.Admin;

public static class AccessCatalog
{
    public static readonly IReadOnlyList<AccessPageItem> Pages =
    [
        new("overview", "Overview"),
        new("operators", "Operators"),
        new("customers", "Customers"),
        new("territories", "Territories"),
        new("fares", "Fare matrix"),
        new("billing", "Billing"),
        new("announcements", "Announcements"),
        new("support", "Support"),
        new("audit", "Audit"),
        new("settings", "Settings")
    ];

    public static readonly HashSet<string> PageIds = Pages.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> AllPageIds { get; } = Pages.Select(x => x.Id).ToList();

    public static bool IsKnown(string pageId) => PageIds.Contains(pageId);
}

public record AccessPageItem(string Id, string Label);
