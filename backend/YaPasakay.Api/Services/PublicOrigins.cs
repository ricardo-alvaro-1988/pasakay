namespace YaPasakay.Api.Services;

public static class PublicOrigins
{
    public static string[] From(IConfiguration config)
    {
        var listed = config.GetSection("CorsOrigins").Get<string[]>() ?? [];
        var origins = new List<string>();
        foreach (var item in listed.Concat([config["PublicOrigin"] ?? ""]))
        {
            var origin = item.Trim().TrimEnd('/');
            if (origin.Length == 0 || origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            origins.Add(origin);
            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || System.Net.IPAddress.TryParse(uri.Host, out _))
            {
                continue;
            }

            if (uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                var apex = $"{uri.Scheme}://{uri.Host[4..]}";
                if (!uri.IsDefaultPort)
                {
                    apex += $":{uri.Port}";
                }

                if (!origins.Contains(apex, StringComparer.OrdinalIgnoreCase))
                {
                    origins.Add(apex);
                }
            }
            else
            {
                var www = $"{uri.Scheme}://www.{uri.Host}";
                if (!uri.IsDefaultPort)
                {
                    www += $":{uri.Port}";
                }

                if (!origins.Contains(www, StringComparer.OrdinalIgnoreCase))
                {
                    origins.Add(www);
                }
            }
        }

        if (origins.Count == 0)
        {
            origins.Add("http://127.0.0.1:5174");
            origins.Add("http://localhost:5174");
        }

        return origins.ToArray();
    }

    public static string Primary(IConfiguration config)
    {
        var origin = (config["PublicOrigin"] ?? string.Empty).Trim().TrimEnd('/');
        return origin.Length > 0 ? origin : "http://127.0.0.1:5174";
    }
}
