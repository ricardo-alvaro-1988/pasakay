using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using YaPasakay.Application.Admin;
using YaPasakay.Domain.Entities;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Api.Services;

/// <summary>
/// Serves index.html / PWA manifest with live platform brand so link-preview crawlers
/// (Messenger, Facebook, etc.) see the configured name instead of the baked-in default.
/// </summary>
public sealed class BrandShell(IServiceScopeFactory scopes, IConfiguration config, IWebHostEnvironment env)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);
    private static readonly Regex TitleRx = new(@"<title>[^<]*</title>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AppleTitleRx = new(
        @"<meta\s+name=[""']apple-mobile-web-app-title[""']\s+content=[""'][^""']*[""']\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ThemeColorRx = new(
        @"<meta\s+name=[""']theme-color[""']\s+content=[""'][^""']*[""']\s*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex IconLinkRx = new(
        @"<link\s+rel=[""']icon[""'][^>]*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex InjectedMetaRx = new(
        @"<!-- brand-meta -->[\s\S]*?<!-- /brand-meta -->",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly object _gate = new();
    private BrandSnapshot? _cached;
    private DateTime _expiresUtc = DateTime.MinValue;

    public void Invalidate()
    {
        lock (_gate)
        {
            _cached = null;
            _expiresUtc = DateTime.MinValue;
        }
    }

    public async Task<bool> TryHandleAsync(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
        {
            return false;
        }

        var path = context.Request.Path.Value ?? "";
        if (path.Equals("/manifest.webmanifest", StringComparison.OrdinalIgnoreCase))
        {
            await WriteManifestAsync(context);
            return true;
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

        if (path is "/" or "/index.html")
        {
            await SendHtmlAsync(
                context,
                Path.Combine(webRoot, "index.html"),
                "Customer app is not published. Run deploy/sync-wwwroot.ps1.",
                ops: false);
            return true;
        }

        if (path is "/ops/" or "/ops/index.html")
        {
            await SendHtmlAsync(
                context,
                Path.Combine(webRoot, "ops", "index.html"),
                "Operator portal is not published. Run deploy/sync-wwwroot.ps1.",
                ops: true);
            return true;
        }

        return false;
    }

    public Task SendCustomerHtmlAsync(HttpContext context, string file, string missing) =>
        SendHtmlAsync(context, file, missing, ops: false);

    public Task SendOpsHtmlAsync(HttpContext context, string file, string missing) =>
        SendHtmlAsync(context, file, missing, ops: true);

    private async Task SendHtmlAsync(HttpContext context, string file, string missing, bool ops)
    {
        if (!File.Exists(file))
        {
            context.Response.StatusCode = 404;
            await context.Response.WriteAsync(missing);
            return;
        }

        var brand = await GetBrandAsync(context.RequestAborted);
        var html = await File.ReadAllTextAsync(file, context.RequestAborted);
        var origin = RequestOrigin(context);
        var pageUrl = origin + (ops ? "/ops/" : "/");
        var title = ops ? $"{brand.BrandName} Admin" : brand.BrandName;
        var imageUrl = origin + (brand.LogoUrl ?? brand.FaviconUrl ?? "/icon.png");
        html = RewriteHtml(html, brand, title, pageUrl, imageUrl);

        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync(html, context.RequestAborted);
    }

    private async Task WriteManifestAsync(HttpContext context)
    {
        var brand = await GetBrandAsync(context.RequestAborted);
        var origin = RequestOrigin(context);
        var icon = origin + (brand.LogoUrl ?? brand.FaviconUrl ?? "/icon.png");
        var payload = new
        {
            name = brand.BrandName,
            short_name = brand.ShortName,
            description = $"Book a motorcycle or tricycle ride with {brand.BrandName}.",
            start_url = "/",
            scope = "/",
            display = "standalone",
            background_color = brand.Accent,
            theme_color = brand.Accent,
            icons = new[]
            {
                new { src = icon, sizes = "512x512", type = "image/png", purpose = "any maskable" }
            }
        };

        context.Response.ContentType = "application/manifest+json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }),
            context.RequestAborted);
    }

    private async Task<BrandSnapshot> GetBrandAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            if (_cached is not null && DateTime.UtcNow < _expiresUtc)
            {
                return _cached;
            }
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.PlatformBrandSettings
            .AsNoTracking()
            .OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        settings ??= new PlatformBrandSettings
        {
            BrandName = BrandThemeCatalog.DefaultBrandName,
            ShortName = BrandThemeCatalog.DefaultShortName,
            ThemeId = BrandThemeCatalog.DefaultThemeId,
        };

        var theme = BrandThemeCatalog.Resolve(settings.ThemeId);
        var snapshot = new BrandSnapshot(
            settings.BrandName.Trim().Length > 0 ? settings.BrandName.Trim() : BrandThemeCatalog.DefaultBrandName,
            settings.ShortName.Trim().Length > 0 ? settings.ShortName.Trim() : BrandThemeCatalog.DefaultShortName,
            theme.Accent,
            UploadUrls.FromPath(settings.LogoPath),
            UploadUrls.FromPath(settings.FaviconPath));

        lock (_gate)
        {
            _cached = snapshot;
            _expiresUtc = DateTime.UtcNow.Add(CacheTtl);
        }

        return snapshot;
    }

    private string RequestOrigin(HttpContext context)
    {
        var configured = PublicOrigins.Primary(config);
        if (configured.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || configured.Contains("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return $"{context.Request.Scheme}://{context.Request.Host.Value}".TrimEnd('/');
        }

        return configured;
    }

    internal static string RewriteHtml(
        string html,
        BrandSnapshot brand,
        string title,
        string pageUrl,
        string imageUrl)
    {
        var t = Enc(title);
        var name = Enc(brand.BrandName);
        var shortName = Enc(brand.ShortName);
        var accent = Enc(brand.Accent);
        var desc = Enc($"Book a motorcycle or tricycle ride with {brand.BrandName}.");
        var image = Enc(imageUrl);
        var url = Enc(pageUrl);

        html = TitleRx.Replace(html, $"<title>{t}</title>");
        if (AppleTitleRx.IsMatch(html))
        {
            html = AppleTitleRx.Replace(html, $"<meta name=\"apple-mobile-web-app-title\" content=\"{shortName}\" />");
        }

        if (ThemeColorRx.IsMatch(html))
        {
            html = ThemeColorRx.Replace(html, $"<meta name=\"theme-color\" content=\"{accent}\" />");
        }
        else
        {
            html = html.Replace("</head>", $"<meta name=\"theme-color\" content=\"{accent}\" />{Environment.NewLine}</head>", StringComparison.OrdinalIgnoreCase);
        }

        var favicon = brand.FaviconUrl ?? "/favicon.png";
        if (IconLinkRx.IsMatch(html))
        {
            html = IconLinkRx.Replace(html, $"<link rel=\"icon\" type=\"image/png\" href=\"{Enc(favicon)}\" />");
        }

        html = InjectedMetaRx.Replace(html, string.Empty);
        var meta = new StringBuilder();
        meta.AppendLine("<!-- brand-meta -->");
        meta.AppendLine($"<meta name=\"description\" content=\"{desc}\" />");
        meta.AppendLine("<meta property=\"og:type\" content=\"website\" />");
        meta.AppendLine($"<meta property=\"og:site_name\" content=\"{name}\" />");
        meta.AppendLine($"<meta property=\"og:title\" content=\"{t}\" />");
        meta.AppendLine($"<meta property=\"og:description\" content=\"{desc}\" />");
        meta.AppendLine($"<meta property=\"og:url\" content=\"{url}\" />");
        meta.AppendLine($"<meta property=\"og:image\" content=\"{image}\" />");
        meta.AppendLine("<meta name=\"twitter:card\" content=\"summary\" />");
        meta.AppendLine($"<meta name=\"twitter:title\" content=\"{t}\" />");
        meta.AppendLine($"<meta name=\"twitter:description\" content=\"{desc}\" />");
        meta.Append("<!-- /brand-meta -->");

        var headClose = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (headClose >= 0)
        {
            html = html.Insert(headClose, meta + Environment.NewLine);
        }

        return html;
    }

    private static string Enc(string value) => WebUtility.HtmlEncode(value);

    internal sealed record BrandSnapshot(
        string BrandName,
        string ShortName,
        string Accent,
        string? LogoUrl,
        string? FaviconUrl);
}
