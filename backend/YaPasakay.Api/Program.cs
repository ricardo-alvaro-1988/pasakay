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
builder.Services.AddScoped<YaPasakay.Api.Services.RiderWalletService>();
builder.Services.AddScoped<YaPasakay.Api.Services.TripBroadcastService>();
builder.Services.AddSingleton<YaPasakay.Api.Services.TripChatRealtime>();
builder.Services.AddScoped<YaPasakay.Api.Services.LiveNotify>();
builder.Services.AddSingleton<YaPasakay.Api.Services.GoogleDrivingDistance>();
builder.Services.AddScoped<YaPasakay.Api.Services.AdminAccessFilter>();
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<YaPasakay.Api.Services.AdminAccessFilter>();
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var seederLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
    var uploadRoot = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads");
    await DbSeeder.SeedAsync(db, seederLogger, uploadRoot);
}

app.UseForwardedHeaders();
app.UseCors(app.Environment.IsDevelopment() ? "dev" : "site");
var chatUploadRoot = Path.Combine(app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot"), "uploads");
Directory.CreateDirectory(chatUploadRoot);
SpaHost.UseStatic(app);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(chatUploadRoot),
    RequestPath = "/uploads",
    ServeUnknownFileTypes = true
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TripChatHub>("/hubs/chat");
app.MapHub<DeskHub>("/hubs/desk");
app.MapHub<OpsHub>("/hubs/ops");
app.MapGet("/health", () => Results.Ok(new { status = "ok", app = "Ya! Pasakay" }));
SpaHost.MapFallbacks(app);

app.Run();
