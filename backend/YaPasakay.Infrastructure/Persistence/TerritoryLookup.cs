using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;

namespace YaPasakay.Infrastructure.Persistence;

public static class TerritoryLookup
{
    public static Task<List<IdName>> ProvincesAsync(AppDbContext db, CancellationToken cancellationToken) =>
        db.Provinces
            .OrderBy(x => x.Name)
            .Select(x => new IdName(x.Id, x.Name))
            .ToListAsync(cancellationToken);

    public static Task<List<IdName>> MunicipalitiesAsync(
        AppDbContext db,
        Guid provinceId,
        CancellationToken cancellationToken) =>
        db.Municipalities
            .Where(x => x.ProvinceId == provinceId)
            .OrderBy(x => x.Name)
            .Select(x => new IdName(x.Id, x.Name))
            .ToListAsync(cancellationToken);

    public static Task<List<BarangayOption>> BarangaysAsync(
        AppDbContext db,
        Guid municipalityId,
        CancellationToken cancellationToken) =>
        db.Barangays
            .Where(x => x.MunicipalityId == municipalityId)
            .OrderBy(x => x.Name)
            .Select(x => new BarangayOption(
                x.Id,
                x.Name,
                x.MunicipalityId,
                x.Municipality.Name,
                x.Municipality.ProvinceId,
                x.Municipality.Province.Name))
            .ToListAsync(cancellationToken);

    public static Task<Barangay?> LoadAsync(AppDbContext db, Guid id, CancellationToken cancellationToken) =>
        db.Barangays
            .Include(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public static async Task<Barangay?> MatchFromAddressAsync(
        AppDbContext db,
        Guid? barangayId,
        string? details,
        CancellationToken cancellationToken)
    {
        if (barangayId is Guid id && id != Guid.Empty)
        {
            var exact = await LoadAsync(db, id, cancellationToken);
            if (exact is not null)
            {
                return exact;
            }
        }

        var parts = SplitAddress(details);
        if (parts.Count == 0)
        {
            return null;
        }

        var provinces = await db.Provinces.ToListAsync(cancellationToken);
        var province = Best(provinces, x => x.Name, parts);

        var municipalities = await db.Municipalities
            .Include(x => x.Province)
            .ToListAsync(cancellationToken);
        if (province is not null)
        {
            municipalities = municipalities.Where(x => x.ProvinceId == province.Id).ToList();
        }

        var municipalityParts = province is null
            ? parts
            : parts.Where(part => PhTerritoryCatalog.Normalize(part) != PhTerritoryCatalog.Normalize(province.Name)).ToList();
        var municipality = Best(municipalities, x => x.Name, municipalityParts);

        List<Barangay> barangays;
        if (municipality is not null)
        {
            barangays = await db.Barangays
                .Include(x => x.Municipality)
                .ThenInclude(x => x.Province)
                .Where(x => x.MunicipalityId == municipality.Id)
                .ToListAsync(cancellationToken);
        }
        else if (province is not null)
        {
            barangays = await db.Barangays
                .Include(x => x.Municipality)
                .ThenInclude(x => x.Province)
                .Where(x => x.Municipality.ProvinceId == province.Id)
                .ToListAsync(cancellationToken);
        }
        else
        {
            return null;
        }

        var barangay = Best(barangays, x => x.Name, parts);
        if (barangay is not null)
        {
            return barangay;
        }

        if (municipality is null)
        {
            return null;
        }

        var covered = await db.OperatorBarangays
            .Include(x => x.Barangay)
            .ThenInclude(x => x.Municipality)
            .ThenInclude(x => x.Province)
            .Where(x => x.Operator.IsActive && x.Barangay.MunicipalityId == municipality.Id)
            .Select(x => x.Barangay)
            .FirstOrDefaultAsync(cancellationToken);
        return covered ?? barangays.OrderBy(x => x.Name).FirstOrDefault();
    }

    public static async Task<Municipality?> MatchMunicipalityFromAddressAsync(
        AppDbContext db,
        string? details,
        CancellationToken cancellationToken)
    {
        var barangay = await MatchFromAddressAsync(db, null, details, cancellationToken);
        if (barangay is not null)
        {
            return barangay.Municipality;
        }

        var parts = SplitAddress(details);
        if (parts.Count == 0)
        {
            return null;
        }

        var provinces = await db.Provinces.ToListAsync(cancellationToken);
        var province = Best(provinces, x => x.Name, parts);
        var municipalities = await db.Municipalities
            .Include(x => x.Province)
            .ToListAsync(cancellationToken);
        if (province is not null)
        {
            municipalities = municipalities.Where(x => x.ProvinceId == province.Id).ToList();
        }

        var municipalityParts = province is null
            ? parts
            : parts.Where(part => PhTerritoryCatalog.Normalize(part) != PhTerritoryCatalog.Normalize(province.Name)).ToList();
        return Best(municipalities, x => x.Name, municipalityParts);
    }

    private static List<string> SplitAddress(string? details)
    {
        if (string.IsNullOrWhiteSpace(details))
        {
            return [];
        }

        return details
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part => RegexDigits(part))
            .Where(part => part.Length > 1 && !part.All(char.IsDigit))
            .ToList();
    }

    private static string RegexDigits(string part)
    {
        var cleaned = part.Replace("Philippines", "", StringComparison.OrdinalIgnoreCase).Trim();
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\b\d{4}\b", " ").Trim();
        return System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
    }

    private static T? Best<T>(IReadOnlyList<T> rows, Func<T, string> name, IReadOnlyList<string> parts)
    {
        return rows
            .Select(row => new { Row = row, Score = ScoreName(parts, name(row)) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => name(x.Row).Length)
            .Select(x => x.Row)
            .FirstOrDefault();
    }

    private static int ScoreName(IReadOnlyList<string> parts, string name)
    {
        var key = PhTerritoryCatalog.Normalize(name);
        if (key.Length < 3)
        {
            return 0;
        }

        var best = 0;
        foreach (var part in parts)
        {
            var value = PhTerritoryCatalog.Normalize(part);
            if (value.Length == 0)
            {
                continue;
            }

            if (value == key)
            {
                return 100;
            }

            if (key.Length >= 5 && ContainsToken(value, key))
            {
                best = Math.Max(best, 80);
            }
            else if (value.Length >= 5 && ContainsToken(key, value))
            {
                best = Math.Max(best, 50);
            }
        }

        return best;
    }

    private static bool ContainsToken(string haystack, string needle)
    {
        if (haystack == needle)
        {
            return true;
        }

        return haystack.StartsWith(needle + " ", StringComparison.Ordinal)
            || haystack.EndsWith(" " + needle, StringComparison.Ordinal)
            || haystack.Contains(" " + needle + " ", StringComparison.Ordinal);
    }
}
