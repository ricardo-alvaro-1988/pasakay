using YaPasakay.Application.Admin;
using YaPasakay.Domain.Enums;

namespace YaPasakay.Api.Services;

public static class FareDiscountRules
{
    public static bool TryParse(string? raw, out FareDiscountKind kind)
    {
        kind = FareDiscountKind.None;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        var key = raw.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("-", "", StringComparison.Ordinal);
        kind = key.ToLowerInvariant() switch
        {
            "none" => FareDiscountKind.None,
            "senior" or "seniorcitizen" => FareDiscountKind.SeniorCitizen,
            "pwd" => FareDiscountKind.Pwd,
            "other" or "others" => FareDiscountKind.Other,
            _ => (FareDiscountKind)(-1),
        };
        return Enum.IsDefined(kind);
    }

    public static string Label(FareDiscountKind kind, string? note = null, int? percent = null, decimal amount = 0)
    {
        var name = kind switch
        {
            FareDiscountKind.SeniorCitizen => "Senior citizen",
            FareDiscountKind.Pwd => "PWD",
            FareDiscountKind.Other => "Others",
            _ => "",
        };
        if (name.Length == 0)
        {
            return name;
        }

        var value = percent is int pct
            ? $"{pct}% off"
            : amount > 0
                ? $"₱{amount:0.##} off"
                : "";
        var reason = kind == FareDiscountKind.Other ? note?.Trim() : null;
        if (!string.IsNullOrWhiteSpace(reason) && value.Length > 0)
        {
            return $"{name} · {reason} · {value}";
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            return $"{name} · {reason}";
        }

        return value.Length > 0 ? $"{name} · {value}" : name;
    }

    public static (decimal CustomerFare, decimal Amount, int? Percent, string? Error) Apply(
        decimal customerFare,
        FareDiscountKind kind,
        string? note,
        bool requireNote,
        string? mode,
        decimal value)
    {
        if (kind == FareDiscountKind.None)
        {
            return (customerFare, 0, null, null);
        }

        if (!Enum.IsDefined(kind))
        {
            return (customerFare, 0, null, "Choose Senior citizen, PWD, or Others.");
        }

        var trimmed = note?.Trim();
        if (kind == FareDiscountKind.Other && requireNote && string.IsNullOrWhiteSpace(trimmed))
        {
            return (customerFare, 0, null, "Say what the other discount is for.");
        }

        if (trimmed is { Length: > 80 })
        {
            return (customerFare, 0, null, "Keep the discount note to 80 characters.");
        }

        if (value != decimal.Truncate(value) || value < 1)
        {
            return (customerFare, 0, null, "Enter a whole number.");
        }

        var whole = (int)value;
        var asPercent = string.Equals(mode, "percent", StringComparison.OrdinalIgnoreCase)
            || mode == "%";
        var asPesos = string.Equals(mode, "amount", StringComparison.OrdinalIgnoreCase)
            || string.Equals(mode, "peso", StringComparison.OrdinalIgnoreCase)
            || mode == "₱";
        if (asPercent)
        {
            if (whole is < 1 or > 100)
            {
                return (customerFare, 0, null, "Percent must be from 1 to 100.");
            }

            var (pay, amount) = OperatorPromoRules.SplitFare(customerFare, whole);
            return (pay, amount, whole, null);
        }

        if (asPesos)
        {
            if (whole > customerFare)
            {
                return (customerFare, 0, null, "Discount cannot be more than the fare.");
            }

            return (CommissionCut.Round(customerFare - whole), whole, null, null);
        }

        return (customerFare, 0, null, "Choose percent or pesos.");
    }
}
