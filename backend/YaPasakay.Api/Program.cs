using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using YaPasakay.Api.Hubs;
using YaPasakay.Api.Services;
using YaPasakay.Infrastructure;
using YaPasakay.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
        | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton<YaPasakay.Api.Services.UploadStore>();
builder.Services.AddSingleton<YaPasakay.Api.Services.BrandShell>();
builder.Services.AddScoped<YaPasakay.Api.Services.RiderWalletService>();
builder.Services.AddScoped<YaPasakay.Api.Services.TripBroadcastService>();
builder.Services.AddSingleton<YaPasakay.Api.Services.TripChatRealtime>();
builder.Services.AddScoped<YaPasakay.Api.Services.OperatorPromoService>();
builder.Services.AddScoped<YaPasakay.Api.Services.DeriveFarePricingService>();
builder.Services.AddScoped<YaPasakay.Api.Services.LiveNotify>();
builder.Services.AddSingleton<YaPasakay.Api.Services.GoogleDrivingDistance>();
builder.Services.AddHostedService<YaPasakay.Api.Services.ScheduleBroadcastHostedService>();
builder.Services.AddHostedService<YaPasakay.Api.Services.TripExpiryHostedService>();
builder.Services.AddScoped<YaPasakay.Api.Services.AdminAccessFilter>();
builder.Services.AddScoped<YaPasakay.Api.Services.OperatorAccessFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<YaPasakay.Api.Services.AdminAccessFilter>();
    options.Filters.AddService<YaPasakay.Api.Services.OperatorAccessFilter>();
}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddOpenApi();
var corsOrigins = PublicOrigins.From(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("site", policy =>
        policy.WithOrigins(corsOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
    options.AddPolicy("dev", policy =>
        policy.SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var jwt = builder.Configuration.GetSection("Jwt");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10_000_000;
    options.ValueCountLimit = 100_000;
});
builder.Services.AddAuthorization();

var app = builder.Build();
var uploadRoot = StoragePaths.UploadRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(uploadRoot);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var migrateLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbMigrate");
    if (!app.Configuration.GetValue("Database:SkipMigrate", false))
    {
        try
        {
            using var migrateCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            await db.Database.MigrateAsync(migrateCts.Token);
        }
        catch (Exception first)
        {
            migrateLogger.LogError(first, "Database migrate failed; applying merchant catalog bootstrap SQL.");
            try
            {
                await MerchantCatalogBootstrap.EnsureAsync(db);
            }
            catch (Exception second)
            {
                migrateLogger.LogCritical(second, "Merchant catalog bootstrap failed.");
            }
        }
    }
    else
    {
        migrateLogger.LogWarning("Database:SkipMigrate is enabled; starting without MigrateAsync.");
    }

    // Always keep Users.MerchantId in sync with the EF model so login/auth queries never 500.
    try
    {
        await MerchantCatalogBootstrap.EnsureUsersMerchantIdAsync(db);
        if (!await MerchantCatalogBootstrap.HasMerchantsTableAsync(db))
        {
            migrateLogger.LogWarning("Merchants table missing; applying merchant catalog bootstrap.");
            await MerchantCatalogBootstrap.EnsureAsync(db);
        }

        await MerchantCatalogBootstrap.EnsureAddonLibraryAsync(db);
    }
    catch (Exception schemaEx)
    {
        migrateLogger.LogCritical(schemaEx, "Failed to ensure merchant-related schema required for AppUser queries.");
    }

    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    try
    {
        await DbSeeder.SeedAsync(db, seederLogger, uploadRoot);
    }
    catch (Exception seedEx)
    {
        migrateLogger.LogCritical(seedEx, "DbSeeder failed; starting API anyway.");
    }
}

app.UseForwardedHeaders();
app.UseCors(app.Environment.IsDevelopment() ? "dev" : "site");
SpaHost.UseStatic(app);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadRoot),
    RequestPath = "/uploads"
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TripChatHub>("/hubs/chat");
app.MapHub<DeskHub>("/hubs/desk");
app.MapHub<OpsHub>("/hubs/ops");
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "Ya! Pasakay" }));
app.MapGet("/Releases", ReleaseStatus.HandleAsync);
SpaHost.MapFallbacks(app);

app.Run();
