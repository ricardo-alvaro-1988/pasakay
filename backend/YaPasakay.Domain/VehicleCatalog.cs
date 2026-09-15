using YaPasakay.Domain.Enums;

namespace YaPasakay.Domain;

/// <summary>Stable platform vehicle category ids and helpers for dual-read with VehicleType.</summary>
public static class VehicleCatalog
{
    public static readonly Guid MotorcycleId = Guid.Parse("a0000001-0000-4000-8000-000000000001");
    public static readonly Guid TricycleId = Guid.Parse("a0000001-0000-4000-8000-000000000002");
    public static readonly Guid SedanId = Guid.Parse("a0000001-0000-4000-8000-000000000003");
    public static readonly Guid MpvId = Guid.Parse("a0000001-0000-4000-8000-000000000004");
    public static readonly Guid SuvId = Guid.Parse("a0000001-0000-4000-8000-000000000005");
    public static readonly Guid VanId = Guid.Parse("a0000001-0000-4000-8000-000000000006");
    public static readonly Guid PickupL300Id = Guid.Parse("a0000001-0000-4000-8000-000000000007");
    public static readonly Guid PickupCargoId = Guid.Parse("a0000001-0000-4000-8000-000000000008");

    public sealed record Preset(
        Guid Id,
        string Code,
        string Name,
        int MaxPassengers,
        string IconKey,
        bool IsCargo,
        VehicleType LegacyEnum,
        int SortOrder,
        bool DefaultEnabled,
        decimal DefaultCommissionPercent);

    public static IReadOnlyList<Preset> PlatformPresets { get; } =
    [
        new(MotorcycleId, "motorcycle", "Motorcycle", 1, "motorcycle", false, VehicleType.Motorcycle, 10, true, 10m),
        new(TricycleId, "tricycle", "Tricycle", 4, "tricycle", false, VehicleType.Tricycle, 20, true, 5m),
        new(SedanId, "sedan", "Sedan", 4, "sedan", false, VehicleType.Sedan, 30, false, 10m),
        new(MpvId, "mpv", "MPV", 6, "mpv", false, VehicleType.Mpv, 40, false, 10m),
        new(SuvId, "suv", "SUV", 6, "suv", false, VehicleType.Suv, 50, false, 10m),
        new(VanId, "van", "Van", 12, "van", false, VehicleType.Van, 60, false, 10m),
        new(PickupL300Id, "pickup-l300", "Pickup L300", 10, "pickup", false, VehicleType.PickupL300, 70, false, 10m),
        new(PickupCargoId, "pickup-cargo", "Pickup (Cargo)", 2, "pickup-cargo", true, VehicleType.PickupCargo, 80, false, 10m),
    ];

    public static Guid IdFor(VehicleType type) =>
        PlatformPresets.FirstOrDefault(x => x.LegacyEnum == type)?.Id
        ?? throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown vehicle type.");

    public static VehicleType? TypeFor(Guid categoryId) =>
        PlatformPresets.FirstOrDefault(x => x.Id == categoryId)?.LegacyEnum;

    public static Preset? PresetFor(VehicleType type) =>
        PlatformPresets.FirstOrDefault(x => x.LegacyEnum == type);

    public static Preset? PresetFor(Guid categoryId) =>
        PlatformPresets.FirstOrDefault(x => x.Id == categoryId);

    public static bool TryParseType(string? value, out VehicleType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        if (Enum.TryParse(value, true, out type) && Enum.IsDefined(type)) return true;
        var code = value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
        var preset = PlatformPresets.FirstOrDefault(x =>
            x.Code.Equals(code, StringComparison.OrdinalIgnoreCase)
            || x.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (preset is null) return false;
        type = preset.LegacyEnum;
        return true;
    }

    public static string ApiName(VehicleType type) => type.ToString();

    public static int ClampPassengers(VehicleType type, bool isCargo, int maxPassengers, int requested)
    {
        if (isCargo || maxPassengers <= 1) return 1;
        return Math.Clamp(requested <= 0 ? 1 : requested, 1, maxPassengers);
    }
}
