namespace YaPasakay.Application.Admin;

public static class TripAddress
{
    /// <summary>Google/place text only — never the matched barangay append.</summary>
    public static string Display(string? details, string? fullAddress)
    {
        var preferred = (details ?? string.Empty).Trim();
        if (preferred.Length == 0)
        {
            preferred = (fullAddress ?? string.Empty).Trim();
        }

        return Clean(preferred);
    }

    /// <summary>
    /// Removes the barangay/municipality/province suffix that was historically
    /// appended after the Google place (often after "Philippines").
    /// </summary>
    public static string Clean(string? address)
    {
        var text = (address ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var philippines = text.IndexOf("Philippines", StringComparison.OrdinalIgnoreCase);
        if (philippines >= 0)
        {
            return text[..(philippines + "Philippines".Length)].Trim().TrimEnd(',', ' ', ';');
        }

        return StripRepeatedTail(text);
    }

    private static string StripRepeatedTail(string text)
    {
        var parts = text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4)
        {
            return text;
        }

        // Format() appended: details..., Barangay, Municipality, Province
        // If the last 2–3 parts already appear earlier, drop the duplicate tail.
        for (var take = 3; take >= 2; take--)
        {
            if (parts.Length <= take)
            {
                continue;
            }

            var tail = parts[^take..];
            var head = parts[..^take];
            if (tail.All(part => head.Any(h => string.Equals(h, part, StringComparison.OrdinalIgnoreCase))))
            {
                return string.Join(", ", head);
            }
        }

        return text;
    }
}
