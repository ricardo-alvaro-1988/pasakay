namespace YaPasakay.Api.Services;

public static class StoragePaths
{
    public static string UploadRoot(IConfiguration config, IWebHostEnvironment environment)
    {
        var configured = FirstConfigured(
            config["Storage:UploadsPath"],
            config["YP_UPLOAD_ROOT"]);

        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = environment.IsProduction() && OperatingSystem.IsLinux()
                ? "/var/lib/yapasakay/uploads"
                : Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "uploads");
        }

        configured = Environment.ExpandEnvironmentVariables(configured);
        if (!Path.IsPathRooted(configured))
        {
            configured = Path.Combine(environment.ContentRootPath, configured);
        }

        return Path.GetFullPath(configured);
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.Select(x => x?.Trim()).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
}
