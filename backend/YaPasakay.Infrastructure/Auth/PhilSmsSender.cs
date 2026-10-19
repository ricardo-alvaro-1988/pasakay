using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YaPasakay.Application.Auth;

namespace YaPasakay.Infrastructure.Auth;

public class PhilSmsSender(HttpClient http, IOptions<PhilSmsOptions> options, ILogger<PhilSmsSender> logger) : IPhilSmsSender
{
    public async Task SendAsync(string phone, string code, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var token = (settings.ApiToken ?? string.Empty).Trim();
        var senderId = (settings.SenderId ?? string.Empty).Trim();
        if (token.Length == 0)
        {
            throw new InvalidOperationException("SMS is not configured.");
        }

        if (senderId.Length is < 1 or > 11)
        {
            throw new InvalidOperationException("SMS sender is not configured.");
        }

        if (phone.Length != 11 || !phone.StartsWith("09", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Enter a valid phone number.");
        }

        var payload = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["recipient"] = "63" + phone[1..],
            ["sender_id"] = senderId,
            ["type"] = "plain",
            ["message"] = $"Your sign-in code is {code}. It expires in 5 minutes."
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://dashboard.philsms.com/api/v3/sms/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || !IsSuccess(body))
        {
            var detail = ReadError(body);
            logger.LogWarning("PhilSMS send failed with status {StatusCode}: {Detail}", (int)response.StatusCode, detail);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(detail)
                    ? "Could not send the sign-in code. Try again."
                    : $"Could not send the sign-in code. {detail}");
        }
    }

    private static bool IsSuccess(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("status", out var status))
            {
                return string.Equals(status.GetString(), "success", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static string ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("message", out var message))
            {
                var text = message.ValueKind == JsonValueKind.String
                    ? message.GetString()
                    : message.ToString();
                return (text ?? string.Empty).Trim();
            }
        }
        catch (JsonException)
        {
            return string.Empty;
        }

        return string.Empty;
    }
}
