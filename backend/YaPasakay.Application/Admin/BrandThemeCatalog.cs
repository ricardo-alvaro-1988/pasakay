namespace YaPasakay.Application.Admin;

public sealed record BrandThemePreset(string Id, string Label, string Accent, string Good);

public static class BrandThemeCatalog
{
    public const string DefaultThemeId = "pasakay-red";
    public const string DefaultBrandName = "Ya! Pasakay";
    public const string DefaultShortName = "Pasakay";

    public static readonly IReadOnlyList<BrandThemePreset> All =
    [
        new("pasakay-red", "Pasakay Red", "#e30613", "#1ea36a"),
        new("crimson-blaze", "Crimson Blaze", "#c41e3a", "#2ecc71"),
        new("ruby-night", "Ruby Night", "#9b1b30", "#3dd68c"),
        new("sunset-orange", "Sunset Orange", "#ff5a1f", "#16a34a"),
        new("amber-ride", "Amber Ride", "#f59e0b", "#059669"),
        new("gold-fleet", "Gold Fleet", "#d4a017", "#0d9488"),
        new("lime-transit", "Lime Transit", "#84cc16", "#15803d"),
        new("forest-green", "Forest Green", "#166534", "#22c55e"),
        new("teal-route", "Teal Route", "#0d9488", "#65a30d"),
        new("ocean-blue", "Ocean Blue", "#0284c7", "#10b981"),
        new("royal-blue", "Royal Blue", "#1d4ed8", "#22c55e"),
        new("indigo-drive", "Indigo Drive", "#4f46e5", "#14b8a6"),
        new("violet-hail", "Violet Hail", "#7c3aed", "#34d399"),
        new("magenta-metro", "Magenta Metro", "#c026d3", "#2dd4bf"),
        new("pink-pulse", "Pink Pulse", "#db2777", "#4ade80"),
        new("rose-city", "Rose City", "#e11d48", "#22c55e"),
        new("slate-steel", "Slate Steel", "#475569", "#22c55e"),
        new("charcoal-pro", "Charcoal Pro", "#1e293b", "#84cc16"),
        new("cyan-signal", "Cyan Signal", "#06b6d4", "#22c55e"),
        new("emerald-go", "Emerald Go", "#059669", "#f59e0b"),
        new("navy-dispatch", "Navy Dispatch", "#1e3a8a", "#fbbf24"),
        new("copper-road", "Copper Road", "#b45309", "#10b981"),
        new("grape-lane", "Grape Lane", "#6d28d9", "#f472b6"),
        new("mint-fresh", "Mint Fresh", "#10b981", "#3b82f6"),
        new("scarlet-dash", "Scarlet Dash", "#dc2626", "#4ade80"),
        new("tangerine-go", "Tangerine Go", "#ea580c", "#0ea5e9"),
        new("honey-cab", "Honey Cab", "#ca8a04", "#0891b2"),
        new("olive-fleet", "Olive Fleet", "#4d7c0f", "#f97316"),
        new("jade-run", "Jade Run", "#047857", "#f59e0b"),
        new("aqua-lane", "Aqua Lane", "#0891b2", "#eab308"),
        new("skyline-blue", "Skyline Blue", "#0369a1", "#84cc16"),
        new("cobalt-ride", "Cobalt Ride", "#1e40af", "#fb923c"),
        new("iris-trip", "Iris Trip", "#5b21b6", "#34d399"),
        new("orchid-hop", "Orchid Hop", "#a21caf", "#22d3ee"),
        new("flamingo-fare", "Flamingo Fare", "#be185d", "#67e8f9"),
        new("berry-book", "Berry Book", "#9f1239", "#86efac"),
        new("storm-gray", "Storm Gray", "#334155", "#38bdf8"),
        new("ink-black", "Ink Black", "#0f172a", "#a3e635"),
        new("sand-route", "Sand Route", "#a8a29e", "#0f766e"),
        new("coral-bay", "Coral Bay", "#f43f5e", "#14b8a6"),
    ];

    private static readonly Dictionary<string, BrandThemePreset> ById =
        All.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public static BrandThemePreset Resolve(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && ById.TryGetValue(themeId.Trim(), out var preset)
            ? preset
            : ById[DefaultThemeId];

    public static bool IsKnown(string? themeId) =>
        !string.IsNullOrWhiteSpace(themeId) && ById.ContainsKey(themeId.Trim());

    public static string AccentSoft(string accentHex, double alpha = 0.16)
    {
        var hex = accentHex.TrimStart('#');
        if (hex.Length != 6)
        {
            return $"rgba(227, 6, 19, {alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
        }

        var r = Convert.ToInt32(hex[..2], 16);
        var g = Convert.ToInt32(hex[2..4], 16);
        var b = Convert.ToInt32(hex[4..6], 16);
        return $"rgba({r}, {g}, {b}, {alpha.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
    }
}
