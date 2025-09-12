using Microsoft.AspNetCore.Mvc;

using NestProxy.Server.Services;
using NestProxy.Shared.Models;

namespace NestProxy.Server.Controllers;

/// <summary>
/// Provides CRUD-like endpoints for listing, enabling and disabling cameras along with an ad-hoc
/// synchronization endpoint to fetch devices from Google SDM.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CamerasController(ICameraRepository cameraRepository, GoogleSdmService sdm, ILogger<CamerasController> logger, IConfiguration config, ICameraRuntimeState runtimeState) : ControllerBase
{
    /// <summary>
    /// Lists cameras with derived local RTSP URLs. Triggers an initial sync if the table is empty.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Camera>>> List(CancellationToken ct)
    {
        // Attempt sync passive (ignore errors) if no cams
        if (!await cameraRepository.AnyAsync(ct))
        {
            logger.LogInformation("Cameras list empty; triggering passive sync.");
            await sdm.SyncCamerasAsync(ct);
        }

        var basePort = config.GetSection("Streaming").GetValue("RtspBasePort", 8554);
        var cams = await cameraRepository.GetAllAsync(ct);
        return cams.Select(c => new Camera
        {
            Id = c.Id,
            DeviceName = c.DeviceName,
            DisplayName = c.DisplayName,
            Room = c.Room,
            IsEnabled = c.IsEnabled,
            LocalRtspUrl = $"rtsp://localhost:{basePort}/{c.Id}",
            IsRunning = runtimeState.IsRunning(c.Id),
            LastError = c.LastError,
            ExpiresAt = c.ExpiresAt
        }).ToList();
    }

    /// <summary>
    /// Performs a sync with Google SDM and returns number of processed cameras.
    /// </summary>
    [HttpPost("sync")]
    public async Task<IActionResult> Sync(CancellationToken ct)
    {
        var count = await sdm.SyncCamerasAsync(ct);
        logger.LogInformation("Cameras sync completed count={Count}", count);
        return Ok(new { count });
    }

    /// <summary>
    /// Enables a camera for streaming.
    /// </summary>
    [HttpPost("{id}/enable")]
    public async Task<IActionResult> Enable(string id, CancellationToken ct)
    {
        var camera = await cameraRepository.GetByIdAsync(id, ct);
        if (camera == null)
        {
            logger.LogWarning("Enable requested for missing camera id={Id}", id);
            return NotFound();
        }

        camera.IsEnabled = true;
        await cameraRepository.SaveChangesAsync(ct);
        logger.LogInformation("Camera enabled id={Id}", id);
        return NoContent();
    }

    /// <summary>
    /// Disables a camera for streaming.
    /// </summary>
    [HttpPost("{id}/disable")]
    public async Task<IActionResult> Disable(string id, CancellationToken ct)
    {
        var camera = await cameraRepository.GetByIdAsync(id, ct);
        if (camera == null)
        {
            logger.LogWarning("Disable requested for missing camera id={Id}", id);
            return NotFound();
        }

        camera.IsEnabled = false;
        await cameraRepository.SaveChangesAsync(ct);
        logger.LogInformation("Camera disabled id={Id}", id);
        return NoContent();
    }
}