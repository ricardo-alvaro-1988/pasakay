using YaPasakay.Domain;
using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class VehicleCountMaps
{
    public static VehicleCountItem ItemFor(VehicleType type, int count, string? code = null, string? name = null)
    {
        if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(name))
        {
            var resolvedCode = !string.IsNullOrWhiteSpace(code)
                ? code.Trim()
                : Slug(name!);
            var resolvedName = !string.IsNullOrWhiteSpace(name) ? name.Trim() : resolvedCode;
            return new VehicleCountItem(resolvedCode, resolvedName, type, count);
        }

        if (type == VehicleType.Custom)
        {
            return new VehicleCountItem("custom", "Custom", type, count);
        }

        var preset = VehicleCatalog.PresetFor(type);
        return preset is not null
            ? new VehicleCountItem(preset.Code, preset.Name, type, count)
            : new VehicleCountItem(type.ToString().ToLowerInvariant(), type.ToString(), type, count);
    }

    public static IReadOnlyList<VehicleCountItem> FromRiders(IEnumerable<RiderProfile> riders) =>
        FromEntries(riders.Select(x => (
            x.VehicleType,
            x.VehicleCategoryId,
            x.VehicleCategory?.Code,
            x.VehicleCategory?.Name)));

    /// <summary>
    /// Groups by VehicleCategoryId when present so distinct custom categories stay separate;
    /// falls back to VehicleType (and category Name/Code when provided).
    /// </summary>
    public static IReadOnlyList<VehicleCountItem> FromEntries(
        IEnumerable<(VehicleType Type, Guid? CategoryId, string? Code, string? Name)> entries)
    {
        return entries
            .GroupBy(BucketKey)
            .Select(g =>
            {
                var first = g.First();
                var type = first.Type;
                var code = first.Code;
                var name = first.Name;

                if (first.CategoryId is Guid categoryId)
                {
                    var preset = VehicleCatalog.PresetFor(categoryId);
                    if (preset is not null)
                    {
                        type = preset.LegacyEnum;
                        code ??= preset.Code;
                        name ??= preset.Name;
                    }
                }
                else if (type != VehicleType.Custom)
                {
                    var preset = VehicleCatalog.PresetFor(type);
                    code ??= preset?.Code;
                    name ??= preset?.Name;
                }

                return ItemFor(type, g.Count(), code, name);
            })
            .Where(x => x.Count > 0)
            .OrderBy(x => SortOrder(x.VehicleType, x.Code))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<VehicleCountItem> FromTypeCounts(IEnumerable<(VehicleType Type, int Count)> counts) =>
        counts
            .GroupBy(x => x.Type)
            .Select(g => ItemFor(g.Key, g.Sum(x => x.Count)))
            .Where(x => x.Count > 0)
            .OrderBy(x => SortOrder(x.VehicleType, x.Code))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string BucketKey((VehicleType Type, Guid? CategoryId, string? Code, string? Name) x)
    {
        if (x.CategoryId is Guid id)
        {
            return $"id:{id:N}";
        }

        // Distinct customs without an id still split by code/name when available.
        if (x.Type == VehicleType.Custom)
        {
            if (!string.IsNullOrWhiteSpace(x.Code))
            {
                return $"custom-code:{x.Code.Trim().ToLowerInvariant()}";
            }

            if (!string.IsNullOrWhiteSpace(x.Name))
            {
                return $"custom-name:{x.Name.Trim().ToLowerInvariant()}";
            }
        }

        return $"type:{(int)x.Type}";
    }

    private static int SortOrder(VehicleType type, string code)
    {
        var preset = VehicleCatalog.PresetFor(type) ?? VehicleCatalog.PlatformPresets.FirstOrDefault(x =>
            x.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return preset?.SortOrder ?? 10_000;
    }

    private static string Slug(string value) =>
        string.Join('-', value.Trim().ToLowerInvariant().Split([' ', '_'], StringSplitOptions.RemoveEmptyEntries));
}
