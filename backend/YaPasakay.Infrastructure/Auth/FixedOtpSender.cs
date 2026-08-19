using Microsoft.Extensions.Logging;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class FixedOtpSender(ILogger<FixedOtpSender> logger) : IOtpSender
{
    public const string DevCode = "1234";

    public Task SendAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Staff OTP for {Phone} is {Code}", phone, code);
        return Task.CompletedTask;
    }
}
