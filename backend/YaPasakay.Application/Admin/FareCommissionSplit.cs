using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public static class FareCommissionSplit
{
    public const decimal DefaultOperatorShare = 20m;

    public static (decimal Operator, decimal Driver) Defaults(decimal systemPercent) =>
        Balance(systemPercent, DefaultOperatorShare);

    public static (decimal Operator, decimal Driver) Balance(decimal systemPercent, decimal operatorShare)
    {
        var system = Round(systemPercent);
        var remaining = Round(100m - system);
        if (remaining < 0)
        {
            remaining = 0;
        }

        var opShare = Round(operatorShare);
        if (opShare < 0)
        {
            opShare = 0;
        }

        if (opShare > remaining)
        {
            opShare = remaining;
        }

        return (opShare, Round(remaining - opShare));
    }

    public static void ApplyDefaults(FareMatrix fare, decimal systemPercent)
    {
        var split = Defaults(systemPercent);
        fare.OperatorCommissionPercent = split.Operator;
        fare.DriverCommissionPercent = split.Driver;
    }

    public static void KeepOperatorShare(FareMatrix fare, decimal systemPercent)
    {
        var split = Balance(systemPercent, fare.OperatorCommissionPercent);
        fare.OperatorCommissionPercent = split.Operator;
        fare.DriverCommissionPercent = split.Driver;
        fare.UpdatedAtUtc = DateTime.UtcNow;
    }

    public static decimal SystemPercent(Operator op, VehicleType vehicleType) =>
        vehicleType == VehicleType.Tricycle ? op.TricycleCommissionPercent : op.MotorcycleCommissionPercent;

    public static string? Validate(decimal systemPercent, decimal operatorShare, decimal driverShare)
    {
        var system = Round(systemPercent);
        var opShare = Round(operatorShare);
        var driver = Round(driverShare);
        if (opShare < 0 || opShare > 100 || driver < 0 || driver > 100)
        {
            return "Operator and driver commission must be between 0 and 100.";
        }

        if (Math.Abs(system + opShare + driver - 100m) > 0.01m)
        {
            return "System, operator, and driver commission must add up to 100%.";
        }

        return null;
    }

    public static decimal Round(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
