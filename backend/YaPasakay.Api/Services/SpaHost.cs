using Microsoft.Extensions.FileProviders;

namespace YaPasakay.Api.Services;

public static class SpaHost
{
    public static void UseStatic(WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath
            ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var opsRoot = Path.Combine(webRoot, "ops");
        Directory.CreateDirectory(webRoot);
        Directory.CreateDirectory(opsRoot);

        app.UseDefaultFiles();
        app.UseStaticFiles();

        var opsFiles = new PhysicalFileProvider(opsRoot);
        app.UseDefaultFiles(new DefaultFilesOptions
        {
            FileProvider = opsFiles,
            RequestPath = "/ops"
        });
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = opsFiles,
            RequestPath = "/ops"
        });
    }

    public static void MapFallbacks(WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath
            ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var opsRoot = Path.Combine(webRoot, "ops");

        app.MapGet("/ops", () => Results.Redirect("/ops/"));

        app.MapFallback("/ops/{**path}", async context =>
        {
            await SendHtmlAsync(context, Path.Combine(opsRoot, "index.html"), "Operator portal is not published. Run deploy/sync-wwwroot.ps1.");
        });

        app.MapFallback(async context =>
        {
            if (IsReserved(context.Request.Path))
            {
                context.Response.StatusCode = 404;
                return;
            }

            await SendHtmlAsync(context, Path.Combine(webRoot, "index.html"), "Customer app is not published. Run deploy/sync-wwwroot.ps1.");
        });
    }

    private static bool IsReserved(PathString path) =>
        path.StartsWithSegments("/api")
        || path.StartsWithSegments("/hubs")
        || path.StartsWithSegments("/uploads")
        || path.StartsWithSegments("/health")
        || path.StartsWithSegments("/openapi");

    private static async Task SendHtmlAsync(HttpContext context, string file, string missing)
    {
        if (!File.Exists(file))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync(missing);
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(file);
    }
}
