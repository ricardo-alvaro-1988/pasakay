using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YaPasakay.Application.Auth;
using YaPasakay.Infrastructure.Auth;
using YaPasakay.Infrastructure.Persistence;

namespace YaPasakay.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.Configure<OtpOptions>(configuration.GetSection(OtpOptions.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<FcmOptions>(configuration.GetSection(FcmOptions.SectionName));

        services.AddSingleton<IOtpStore, MemoryOtpStore>();
        services.AddSingleton<IOtpSender, FixedOtpSender>();
        services.AddSingleton<GoogleTokenValidator>();
        services.AddScoped<IPushNotifier, FcmPushNotifier>();
        services.AddSingleton<ITokenService, TokenService>();
        return services;
    }
}
