using YaPasakay.Domain.Entities;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Application.Admin;

public record RideCommissionBreakdown(
    decimal SystemPercent,
    decimal SystemAmount,
    decimal OperatorPercent,
    decimal OperatorAmount,
    decimal DriverPercent,
    decimal DriverAmount);

public static class RideCommissionCalculator
{
    public static RideCommissionBreakdown? ForTrip(Trip trip, FareMatrix? fareMatrix) =>
        ForTrip(trip, trip.Operator, fareMatrix);

    public static RideCommissionBreakdown? ForTrip(Trip trip, Operator op, FareMatrix? fareMatrix)
    {
        if (trip.Status == TripStatus.Cancelled || trip.Fare <= 0)
        {
            return null;
        }

        var systemPercent = FareCommissionSplit.SystemPercent(op, trip.VehicleType);
        decimal operatorPercent;
        decimal driverPercent;
        if (fareMatrix is not null)
        {
            operatorPercent = fareMatrix.OperatorCommissionPercent;
            driverPercent = fareMatrix.DriverCommissionPercent;
        }
        else
        {
            var defaults = FareCommissionSplit.Defaults(systemPercent);
            operatorPercent = defaults.Operator;
            driverPercent = defaults.Driver;
        }

        return new RideCommissionBreakdown(
            systemPercent,
            CommissionCut.Round(trip.Fare * systemPercent / 100m),
            operatorPercent,
            CommissionCut.Round(trip.Fare * operatorPercent / 100m),
            driverPercent,
            CommissionCut.Round(trip.Fare * driverPercent / 100m));
    }

    /// <summary>Rider wallet debit: admin + operator share (matches rider "System" view).</summary>
    public static (decimal Amount, decimal RemitPercent)? WalletDeduction(
        Trip trip,
        Operator op,
        FareMatrix? fareMatrix)
    {
        var breakdown = ForTrip(trip, op, fareMatrix);
        if (breakdown is null)
        {
            return null;
        }

        var remitPercent = FareCommissionSplit.Round(breakdown.SystemPercent + breakdown.OperatorPercent);
        var amount = CommissionCut.Round(breakdown.SystemAmount + breakdown.OperatorAmount);
        return amount <= 0 ? null : (amount, remitPercent);
    }

    public static (decimal SystemAmount, decimal OperatorAmount, decimal DriverAmount) Sum(
        IEnumerable<Trip> trips,
        IReadOnlyDictionary<(Guid OperatorId, VehicleType VehicleType, Guid MunicipalityId), FareMatrix> fares)
    {
        decimal system = 0;
        decimal op = 0;
        decimal driver = 0;
        foreach (var trip in trips)
        {
            var municipalityId = trip.PickupBarangay?.MunicipalityId;
            FareMatrix? fare = null;
            if (municipalityId is Guid mid)
            {
                fares.TryGetValue((trip.OperatorId, trip.VehicleType, mid), out fare);
            }

            var breakdown = ForTrip(trip, fare);
            if (breakdown is null)
            {
                continue;
            }

            system += breakdown.SystemAmount;
            op += breakdown.OperatorAmount;
            driver += breakdown.DriverAmount;
        }

        return (CommissionCut.Round(system), CommissionCut.Round(op), CommissionCut.Round(driver));
    }
}
