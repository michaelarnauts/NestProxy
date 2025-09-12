using Microsoft.AspNetCore.Mvc;

namespace NestProxy.Server.Controllers;

/// <summary>
/// Exposes aggregated runtime metrics for cameras and client sessions including RTP/RTCP counters per payload type.
/// </summary>
/// <remarks>Create a new <see cref="MetricsController"/>.</remarks>
[ApiController]
[Route("api/[controller]")]
public sealed class MetricsController(CameraRegistry cameras, StatisticsReporter stats) : ControllerBase
{
    /// <summary>
    /// Returns a JSON document containing camera and session counts, per-session RTP/RTCP counters and an
    /// aggregate structure grouping per camera and session across payload types.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        DateTime now = DateTime.UtcNow;
        var cameraSummaries = cameras.Streams.Select(camera => new
        {
            cameraId = camera.CameraId,
            sessionCount = camera.Sessions.Count,
            sessions = camera.Sessions.Select(session => new
            {
                session.SessionId,
                session.IsPlaying,
                session.UseUdp,
                lastActivityAgeSeconds = (now - session.LastActivityUtc).TotalSeconds,
                rtpRtcp = stats.TrySnapshot(camera.CameraId, session.SessionId)
            })
        }).ToList();

        var payload = new
        {
            totalCameras = cameraSummaries.Count,
            totalSessions = cameraSummaries.Sum(c => c.sessionCount),
            cameras = cameraSummaries,
            aggregate = stats.SnapshotAll()
        };

        return Ok(payload);
    }
}