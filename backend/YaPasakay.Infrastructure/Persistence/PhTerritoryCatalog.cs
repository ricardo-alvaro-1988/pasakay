using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace YaPasakay.Infrastructure.Persistence;

internal static class PhTerritoryCatalog
{
    public record Lgu(string Province, string Name);
    public record BarangayRow(string Province, string Municipality, string Name);

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Lazy<IReadOnlyList<Lgu>> LazyLgus = new(LoadLgus);
    private static readonly Lazy<IReadOnlyList<BarangayRow>> LazyBarangays = new(LoadBarangays);

    public static IReadOnlyList<Lgu> Lgus => LazyLgus.Value;
    public static IReadOnlyList<BarangayRow> Barangays => LazyBarangays.Value;

    public static string Key(string province, string name) =>
        $"{Normalize(DisplayProvince(province))}|{Normalize(name)}";

    public static string DisplayProvince(string? province)
    {
        if (string.IsNullOrWhiteSpace(province) ||
            province.Contains("NATIONAL CAPITAL", StringComparison.OrdinalIgnoreCase) ||
            province.Equals("NCR", StringComparison.OrdinalIgnoreCase))
        {
            return "Metro Manila";
        }

        return Regex.Replace(province.Trim(), @"\s+", " ");
    }

    public static string DisplayLgu(string name)
    {
        var n = Regex.Replace(name.Trim(), @"\s*\([^)]*\)\s*", " ");
        n = Regex.Replace(n, @"\s+", " ").Trim();
        if (Regex.IsMatch(n, @"^City Of\s+", RegexOptions.IgnoreCase))
        {
            n = Regex.Replace(n, @"^City Of\s+", "", RegexOptions.IgnoreCase).Trim();
            if (!n.EndsWith(" City", StringComparison.OrdinalIgnoreCase))
            {
                n += " City";
            }
        }

        return n;
    }

    public static string DisplayBarangay(string name)
    {
        var n = Regex.Replace(name.Trim(), @"\s*\(Poblacion\)\s*$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"\s*\(Pob\.?\)\s*$", "", RegexOptions.IgnoreCase);
        return Regex.Replace(n, @"\s+", " ").Trim();
    }

    public static string Normalize(string value)
    {
        var n = DisplayLgu(value);
        n = n.Replace('-', ' ');
        n = Regex.Replace(n, @"^City Of\s+", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"\s+City$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, @"\s+", " ");
        return n.Trim().ToUpperInvariant();
    }

    public static string NormalizeBarangay(string name) =>
        DisplayBarangay(name).ToUpperInvariant();

    private static IReadOnlyList<Lgu> LoadLgus()
    {
        var cities = Deserialize<List<PsgcLguRow>>("ph-cities.json") ?? [];
        var munis = Deserialize<List<PsgcLguRow>>("ph-municipalities.json") ?? [];
        var list = new List<Lgu>(cities.Count + munis.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in cities.Concat(munis))
        {
            var name = DisplayLgu(row.Name ?? "");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var province = DisplayProvince(row.Province);
            if (!seen.Add(Key(province, name)))
            {
                continue;
            }

            list.Add(new Lgu(province, name));
        }

        return list
            .OrderBy(x => x.Province, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<BarangayRow> LoadBarangays()
    {
        var raw = Deserialize<List<PsgcBarangayRow>>("ph-barangays.json") ?? [];
        var list = new List<BarangayRow>(raw.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var row in raw)
        {
            var name = DisplayBarangay(row.Name ?? "");
            var municipality = DisplayLgu(row.Municipality ?? "");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(municipality))
            {
                continue;
            }

            var province = DisplayProvince(row.Province);
            var key = $"{Key(province, municipality)}|{NormalizeBarangay(name)}";
            if (!seen.Add(key))
            {
                continue;
            }

            list.Add(new BarangayRow(province, municipality, name));
        }

        return list;
    }

    private static T? Deserialize<T>(string fileName)
    {
        var json = ReadEmbedded(fileName);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static string ReadEmbedded(string fileName)
    {
        var asm = Assembly.GetExecutingAssembly();
        var resource = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Embedded resource '{fileName}' was not found.");
        using var stream = asm.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Cannot open embedded resource '{resource}'.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed class PsgcLguRow
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("province")]
        public string? Province { get; set; }
    }

    private sealed class PsgcBarangayRow
    {
        [JsonPropertyName("province")]
        public string? Province { get; set; }

        [JsonPropertyName("municipality")]
        public string? Municipality { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}
