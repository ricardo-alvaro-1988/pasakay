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
    public static RideCommissionBreakdown? ForTrip(Trip trip, FareMatrix? fareMatrix, DeriveFareMatrix? deriveMatrix = null) =>
        ForTrip(trip, trip.Operator, fareMatrix, deriveMatrix);

    public static RideCommissionBreakdown? ForTrip(Trip trip, Operator op, FareMatrix? fareMatrix, DeriveFareMatrix? deriveMatrix = null)
    {
        var fare = SettlementFare(trip);
        if (trip.Status == TripStatus.Cancelled || fare <= 0)
        {
            return null;
        }

        var systemPercent = FareCommissionSplit.SystemPercent(op, trip.VehicleType);
        decimal operatorPercent;
        decimal driverPercent;
        if (deriveMatrix is not null)
        {
            operatorPercent = deriveMatrix.OperatorCommissionPercent;
            driverPercent = deriveMatrix.DriverCommissionPercent;
        }
        else if (fareMatrix is not null)
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
            CommissionCut.Round(fare * systemPercent / 100m),
            operatorPercent,
            CommissionCut.Round(fare * operatorPercent / 100m),
            driverPercent,
            CommissionCut.Round(fare * driverPercent / 100m));
    }

    /// <summary>
    /// Fare after a rider discount. Older rows kept the original in Fare and the net in CustomerFare.
    /// </summary>
    public static decimal SettlementFare(Trip trip) =>
        SettlementFare(trip.Fare, trip.CustomerFare, trip.FareDiscountAmount);

    public static decimal SettlementFare(decimal fare, decimal customerFare, decimal fareDiscountAmount)
    {
        if (fareDiscountAmount > 0 && customerFare > 0 && customerFare < fare)
        {
            return customerFare;
        }

        return fare;
    }

    /// <summary>Rider wallet debit: admin + operator share (matches rider "System" view).</summary>
    public static (decimal Amount, decimal RemitPercent)? WalletDeduction(
        Trip trip,
        Operator op,
        FareMatrix? fareMatrix,
        DeriveFareMatrix? deriveMatrix = null)
    {
        var breakdown = ForTrip(trip, op, fareMatrix, deriveMatrix);
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
        FareMatrixLookup fares)
    {
        decimal system = 0;
        decimal op = 0;
        decimal driver = 0;
        foreach (var trip in trips)
        {
            var municipalityId = trip.PickupBarangay?.MunicipalityId;
            FareMatrix? fare = municipalityId is Guid mid
                ? fares.Resolve(trip, mid)
                : null;

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
