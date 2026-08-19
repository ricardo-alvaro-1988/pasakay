namespace YaPasakay.Application.Admin;

public static class GovernmentIdCatalog
{
    public static readonly IReadOnlyList<string> All =
    [
        "Driver's License",
        "Professional Driver's License",
        "Passport",
        "PhilSys National ID",
        "UMID",
        "PRC ID",
        "SSS ID",
        "GSIS eCard",
        "TIN ID",
        "PhilHealth ID",
        "Postal ID",
        "Voter's ID"
    ];

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        All.Contains(value.Trim(), StringComparer.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        All.First(item => item.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
}
