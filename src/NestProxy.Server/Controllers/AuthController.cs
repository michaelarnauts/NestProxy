using Microsoft.AspNetCore.Mvc;

using NestProxy.Server.Services;

namespace NestProxy.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IGoogleOAuthService oauthService, ILogger<AuthController> logger) : ControllerBase
{
    [HttpGet("login")]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        var state = Guid.NewGuid().ToString("N");
        logger.LogInformation("Auth login initiated state={State}", state);
        var url = await oauthService.BuildAuthorizationUrlAsync(state, ct);
        return Redirect(url);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        logger.LogInformation("Auth callback received state={State}", state);
        await oauthService.ExchangeCodeForTokensAsync(code, ct);
        return Redirect("/cameras");
    }
}