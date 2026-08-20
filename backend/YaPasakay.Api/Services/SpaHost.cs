using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.StaticFiles;

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

        app.Use(async (context, next) =>
        {
            if ((HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && string.Equals(context.Request.Path.Value, "/ops", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect("/ops/");
                return;
            }

            await next();
        });

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

        app.MapFallback("/ops/{**path}", async context =>
        {
            if (await TrySendFileAsync(context, opsRoot, "/ops"))
            {
                return;
            }

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

    private static async Task<bool> TrySendFileAsync(HttpContext context, string root, string requestPrefix)
    {
        var relative = context.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(relative) ||
            !relative.StartsWith(requestPrefix + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relative = relative[requestPrefix.Length..].TrimStart('/');
        if (string.IsNullOrWhiteSpace(relative) || relative.EndsWith('/'))
        {
            return false;
        }

        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(full))
        {
            return false;
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(full, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        context.Response.ContentType = contentType;
        await context.Response.SendFileAsync(full);
        return true;
    }
}
