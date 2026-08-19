using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class CommissionCut
{
    public static decimal Of(decimal fare, VehicleType vehicleType, decimal motorcyclePercent, decimal tricyclePercent)
    {
        var percent = vehicleType == VehicleType.Motorcycle ? motorcyclePercent : tricyclePercent;
        return fare * percent / 100m;
    }

    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
