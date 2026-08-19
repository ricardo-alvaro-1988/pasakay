using System.Text.RegularExpressions;

namespace YaPasakay.Application.Common;

public static class PhoneNormalizer
{
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        return Regex.Replace(phone, @"\D", string.Empty);
    }

    public static bool TryNormalizePhMobile(string? phone, out string normalized, out string? error)
    {
        normalized = string.Empty;
        var digits = Normalize(phone);
        if (digits.StartsWith("63") && digits.Length == 12)
        {
            digits = "0" + digits[2..];
        }
        else if (digits.Length == 10 && digits.StartsWith('9'))
        {
            digits = "0" + digits;
        }

        if (digits.Length == 11 && digits.StartsWith("09") && digits.All(char.IsDigit))
        {
            normalized = digits;
            error = null;
            return true;
        }

        error = "Enter a valid Philippine mobile number (09XX XXX XXXX).";
        return false;
    }
}
