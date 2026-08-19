using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YaPasakay.Api.Services;

namespace YaPasakay.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicMapsController(IConfiguration config) : ControllerBase
{
    [HttpGet("maps")]
    public ActionResult Maps()
    {
        var key = config["Maps:BrowserApiKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            key = config["Maps:GoogleApiKey"] ?? "";
        }

        return Ok(new
        {
            googleMapsBrowserKey = key.Trim(),
            publicOrigin = PublicOrigins.Primary(config)
        });
    }

    [HttpGet("auth")]
    public ActionResult Auth()
    {
        var clientId = (config["GoogleAuth:ClientId"] ?? string.Empty).Trim();
        return Ok(new
        {
            googleClientId = clientId,
            publicOrigin = PublicOrigins.Primary(config)
        });
    }
}
