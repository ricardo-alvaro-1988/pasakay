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
    try
    {
        await db.Database.MigrateAsync();
    }
    catch (Exception first)
    {
        migrateLogger.LogError(first, "Database migrate failed; repairing merchant catalog leftovers and retrying once.");
        try
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'[ProductAddonOptions]', N'U') IS NOT NULL DROP TABLE [ProductAddonOptions];
                IF OBJECT_ID(N'[ProductAddonGroups]', N'U') IS NOT NULL DROP TABLE [ProductAddonGroups];
                IF OBJECT_ID(N'[MerchantProducts]', N'U') IS NOT NULL DROP TABLE [MerchantProducts];
                IF OBJECT_ID(N'[MerchantProductCategories]', N'U') IS NOT NULL DROP TABLE [MerchantProductCategories];
                IF OBJECT_ID(N'[MerchantOperatingHours]', N'U') IS NOT NULL DROP TABLE [MerchantOperatingHours];
                IF OBJECT_ID(N'[Merchants]', N'U') IS NOT NULL DROP TABLE [Merchants];
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Users_MerchantId' AND object_id = OBJECT_ID(N'[Users]')
                )
                    DROP INDEX [IX_Users_MerchantId] ON [Users];
                IF COL_LENGTH(N'Users', N'MerchantId') IS NOT NULL
                    ALTER TABLE [Users] DROP COLUMN [MerchantId];
                DELETE FROM [__EFMigrationsHistory]
                WHERE [MigrationId] = N'20260911014037_OperatorMerchantCatalog';
                """);
            await db.Database.MigrateAsync();
        }
        catch (Exception second)
        {
            // Keep the API up for login/rides even if merchant catalog migrate is blocked.
            migrateLogger.LogCritical(second, "Database migrate still failing after repair; starting API without pending merchant migration.");
        }
    }

    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    await DbSeeder.SeedAsync(db, seederLogger, uploadRoot);
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
