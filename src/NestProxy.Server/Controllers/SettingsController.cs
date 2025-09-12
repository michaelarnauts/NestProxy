using Microsoft.AspNetCore.Mvc;

using NestProxy.Server.Models;
using NestProxy.Server.Services;

namespace NestProxy.Server.Controllers;

/// <summary>
/// Exposes settings endpoints for retrieving and updating Google authentication configuration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SettingsController(IGoogleSettingsService settingsService) : ControllerBase
{
    /// <summary>
    /// Returns the current (masked) Google authentication options.
    /// </summary>
    [HttpGet("google")]
    public async Task<IActionResult> GetGoogle(CancellationToken ct)
    {
        GoogleAuthOptions options = await settingsService.GetCurrentOptionsAsync(ct);
        return Ok(options);
    }

    /// <summary>
    /// Updates persisted Google authentication settings.
    /// </summary>
    [HttpPost("google")]
    public async Task<IActionResult> UpdateGoogle([FromBody] GoogleAuthOptions opts, CancellationToken ct)
    {
        await settingsService.UpdateAsync(opts, ct);
        return NoContent();
    }
}