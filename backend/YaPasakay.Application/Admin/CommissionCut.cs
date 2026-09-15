using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class CommissionCut
{
    public static decimal Of(decimal fare, VehicleType vehicleType, decimal motorcyclePercent, decimal tricyclePercent)
    {
        var percent = vehicleType == VehicleType.Tricycle ? tricyclePercent : motorcyclePercent;
        if (vehicleType is VehicleType.Motorcycle or VehicleType.Tricycle)
        {
            return fare * percent / 100m;
        }

        // Non-legacy types fall back to motorcycle % when offer % is not supplied via Of(op, …).
        return fare * motorcyclePercent / 100m;
    }

    public static decimal Of(decimal fare, VehicleType vehicleType, Operator op) =>
        Of(fare, FareCommissionSplit.SystemPercent(op, vehicleType));

    public static decimal Of(decimal fare, decimal commissionPercent) =>
        fare * commissionPercent / 100m;

    public static decimal Round(decimal amount) =>
        Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
