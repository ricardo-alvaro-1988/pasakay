namespace YaPasakay.Application.Admin;

public static class TripAddress
{
    /// <summary>Google/place text only — never the matched barangay append.</summary>
    public static string Display(string? details, string? fullAddress)
    {
        var text = (details ?? string.Empty).Trim();
        if (text.Length > 0)
        {
            return text;
        }

        return (fullAddress ?? string.Empty).Trim();
    }
}
