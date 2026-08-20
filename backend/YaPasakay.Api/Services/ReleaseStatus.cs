using System.Net;
using System.Reflection;
using System.Text.Json;

namespace YaPasakay.Api.Services;

public static class ReleaseStatus
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IResult> HandleAsync(HttpContext context, IConfiguration config, IWebHostEnvironment environment)
    {
        var metadata = await ReadAsync(config, environment, context.RequestAborted);
        if (WantsJson(context.Request))
        {
            return Results.Json(metadata);
        }

        return Results.Content(BuildHtml(metadata), "text/html; charset=utf-8");
    }

    private static async Task<object> ReadAsync(IConfiguration config, IWebHostEnvironment environment, CancellationToken cancellationToken)
    {
        var path = StoragePaths.ReleaseMetadataPath(config, environment);
        var assemblyVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!File.Exists(path))
        {
            return new ReleaseView(
                App: "Ya! Pasakay",
                Version: assemblyVersion ?? "Not set",
                UpdatedAtUtc: null,
                UpdatedAtDisplay: "Not published by Jenkins yet",
                BuildNumber: null,
                Commit: null,
                Package: null,
                Environment: environment.EnvironmentName);
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var file = await JsonSerializer.DeserializeAsync<ReleaseFile>(stream, JsonOptions, cancellationToken);
            return new ReleaseView(
                App: string.IsNullOrWhiteSpace(file?.App) ? "Ya! Pasakay" : file.App!.Trim(),
                Version: string.IsNullOrWhiteSpace(file?.Version) ? assemblyVersion ?? "Not set" : file.Version!.Trim(),
                UpdatedAtUtc: BlankToNull(file?.UpdatedAtUtc),
                UpdatedAtDisplay: FormatDate(file?.UpdatedAtUtc),
                BuildNumber: BlankToNull(file?.BuildNumber),
                Commit: BlankToNull(file?.Commit),
                Package: BlankToNull(file?.Package),
                Environment: environment.EnvironmentName);
        }
        catch
        {
            return new ReleaseView(
                App: "Ya! Pasakay",
                Version: assemblyVersion ?? "Not set",
                UpdatedAtUtc: null,
                UpdatedAtDisplay: "Release file cannot be read",
                BuildNumber: null,
                Commit: null,
                Package: null,
                Environment: environment.EnvironmentName);
        }
    }

    private static bool WantsJson(HttpRequest request) =>
        string.Equals(request.Query["format"], "json", StringComparison.OrdinalIgnoreCase) ||
        request.Headers.Accept.Any(x => x?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true);

    private static string BuildHtml(object value)
    {
        var release = (ReleaseView)value;
        var commit = string.IsNullOrWhiteSpace(release.Commit) ? "Not set" : release.Commit;
        if (commit.Length > 12)
        {
            commit = commit[..12];
        }

        return $$"""
            <!doctype html>
            <html lang="en">
              <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Ya! Pasakay Releases</title>
                <style>
                  :root { color-scheme: light dark; font-family: Arial, Helvetica, sans-serif; }
                  body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f6f7f9; color: #14161a; }
                  main { width: min(560px, calc(100% - 32px)); padding: 28px; border: 1px solid #d9dde5; border-radius: 8px; background: #fff; }
                  h1 { margin: 0 0 6px; font-size: 24px; }
                  p { margin: 0 0 24px; color: #5f6673; }
                  dl { display: grid; grid-template-columns: 150px 1fr; gap: 12px 18px; margin: 0; }
                  dt { color: #5f6673; }
                  dd { margin: 0; font-weight: 700; word-break: break-word; }
                  @media (prefers-color-scheme: dark) {
                    body { background: #0f1115; color: #f3f5f8; }
                    main { background: #171a21; border-color: #2a2f3a; }
                    p, dt { color: #a5adba; }
                  }
                </style>
              </head>
              <body>
                <main>
                  <h1>{{Html(release.App)}} Releases</h1>
                  <p>Current production release.</p>
                  <dl>
                    <dt>Version</dt><dd>{{Html(release.Version)}}</dd>
                    <dt>Last update</dt><dd>{{Html(release.UpdatedAtDisplay)}}</dd>
                    <dt>Build</dt><dd>{{Html(release.BuildNumber ?? "Not set")}}</dd>
                    <dt>Commit</dt><dd>{{Html(commit)}}</dd>
                    <dt>Environment</dt><dd>{{Html(release.Environment)}}</dd>
                  </dl>
                </main>
              </body>
            </html>
            """;
    }

    private static string FormatDate(string? value)
    {
        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        }

        return string.IsNullOrWhiteSpace(value) ? "Not set" : value.Trim();
    }

    private static string? BlankToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Html(string value) => WebUtility.HtmlEncode(value);

    private sealed record ReleaseFile(
        string? App,
        string? Version,
        string? UpdatedAtUtc,
        string? BuildNumber,
        string? Commit,
        string? Package);

    private sealed record ReleaseView(
        string App,
        string Version,
        string? UpdatedAtUtc,
        string UpdatedAtDisplay,
        string? BuildNumber,
        string? Commit,
        string? Package,
        string Environment);
}
