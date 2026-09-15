using YaPasakay.Domain;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class VehicleTypeRules
{
    public static bool IsKnown(VehicleType type) =>
        type is not VehicleType.Custom && Enum.IsDefined(type);

    public static bool IsKnownOrCustom(VehicleType type) =>
        type == VehicleType.Custom || IsKnown(type);

    public static string? ValidateChoice(VehicleType type) =>
        IsKnown(type) || type == VehicleType.Custom
            ? null
            : "Choose a valid vehicle type.";

    public static int MaxPassengers(VehicleType type)
    {
        var preset = VehicleCatalog.PresetFor(type);
        return preset?.MaxPassengers ?? (type == VehicleType.Tricycle ? 4 : 1);
    }

    public static bool IsCargo(VehicleType type) =>
        VehicleCatalog.PresetFor(type)?.IsCargo ?? false;

    public static int ClampPassengers(VehicleType type, int requested) =>
        VehicleCatalog.ClampPassengers(type, IsCargo(type), MaxPassengers(type), requested);

    public static int ClampPassengers(VehicleType type, bool isCargo, int maxPassengers, int requested) =>
        VehicleCatalog.ClampPassengers(type, isCargo, maxPassengers, requested);

    public static bool UsesSinglePassengerTier(VehicleType type) =>
        IsCargo(type) || MaxPassengers(type) <= 1;

    public static bool UsesSinglePassengerTier(bool isCargo, int maxPassengers) =>
        isCargo || maxPassengers <= 1;

    public static string Label(VehicleType type) =>
        type == VehicleType.Custom
            ? "Custom"
            : VehicleCatalog.PresetFor(type)?.Name ?? type.ToString();
}
