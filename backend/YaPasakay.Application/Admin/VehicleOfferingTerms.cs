namespace YaPasakay.Application.Admin;

public static class VehicleOfferingTerms
{
    public const string Version = "2026-09-13-v1";

    public const string Text =
        "Offering this type of vehicle requires additional permits and regulatory compliance for your fleet. "
        + "The system and platform are not liable for any fines, penalties, claims, accidents, or legal issues "
        + "arising from operating this vehicle type without proper authority or insurance. "
        + "By continuing, you confirm that you hold the required permits and accept full responsibility for this offering.";

    public static bool RequiresTermsAcceptance(Domain.Enums.VehicleType vehicleType) =>
        vehicleType is not Domain.Enums.VehicleType.Motorcycle and not Domain.Enums.VehicleType.Tricycle;

    public static bool DefaultOffered(Domain.Enums.VehicleType vehicleType) =>
        vehicleType is Domain.Enums.VehicleType.Motorcycle or Domain.Enums.VehicleType.Tricycle;
}
