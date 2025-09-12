using Microsoft.AspNetCore.Mvc;

using NestProxy.Server.Core;
using NestProxy.Server.Data;

namespace NestProxy.Server.Controllers;

/// <summary>
/// Provides simple status and summary information about current camera configuration.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class StatusController(ICameraRepository cameras, ICameraRuntimeState runtimeState) : ControllerBase
{
    /// <summary>
    /// Returns summary counts (total, enabled, running) for cameras.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        List<CameraEntity> all = await cameras.GetAllAsync(ct);
        int enabled = all.Count(c => c.IsEnabled);
        return Ok(new { TotalCameras = all.Count, Enabled = enabled, Running = runtimeState.RunningCount });
    }
}