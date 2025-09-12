using System.Collections.Concurrent;

using Microsoft.Extensions.Options;

namespace NestProxy.Server.Core;

/// <summary>
/// Aggregates downstream RTP / RTCP packet counts per (camera, session, payloadType) over a sliding time window
/// and emits periodic structured log entries. Also provides snapshot APIs used by the metrics endpoint.
/// </summary>
public sealed class StatisticsReporter(CameraRegistry cameraRegistry, IOptions<StreamingOptions> options, ILogger<StatisticsReporter> logger) : IStatisticsReporter
{
    private sealed class DownstreamStats
    {
        public long Rtp;
        public long Rtcp;
        public DateTime WindowStart = DateTime.UtcNow;
    }

    private readonly CameraRegistry cameras = cameraRegistry;
    private readonly ConcurrentDictionary<(string path, string session, int payload), DownstreamStats> _stats = new();
    private readonly TimeSpan _window = TimeSpan.FromSeconds(options.Value.StatsWindowSeconds);

    /// <summary>
    /// Increments the RTP or RTCP counter for the specified key and triggers a log emission if the aggregation
    /// window has elapsed since the last emission for that key.
    /// </summary>
    /// <param name="cameraPath">Logical camera path.</param>
    /// <param name="sessionId">Downstream session identifier.</param>
    /// <param name="payloadType">RTP payload type, or -1 for RTCP-only counts.</param>
    /// <param name="isRtp"><c>true</c> to increment RTP counter, otherwise RTCP counter.</param>
    public void Increment(string cameraPath, string sessionId, int payloadType, bool isRtp)
    {
        DownstreamStats entry = _stats.GetOrAdd((cameraPath, sessionId, payloadType), _ => new DownstreamStats());
        if (isRtp)
        {
            Interlocked.Increment(ref entry.Rtp);
        }
        else
        {
            Interlocked.Increment(ref entry.Rtcp);
        }

        MaybeLog(cameraPath, sessionId, payloadType, entry);
    }

    /// <summary>
    /// Forces logging of all non-zero counters irrespective of window timing (used primarily by tests).
    /// </summary>
    public void FlushAll()
    {
        foreach (var kvp in _stats)
        {
            DownstreamStats snapshot = kvp.Value;
            long rtp = Interlocked.Read(ref snapshot.Rtp);
            long rtcp = Interlocked.Read(ref snapshot.Rtcp);
            if (rtp == 0 && rtcp == 0)
            {
                continue;
            }

            Log(kvp.Key.path, kvp.Key.session, kvp.Key.payload, rtp, rtcp);
        }
    }

    /// <summary>
    /// Produces a snapshot of aggregated counters for a single session grouped by payload type.
    /// </summary>
    /// <param name="cameraPath">Camera path.</param>
    /// <param name="sessionId">Session identifier.</param>
    /// <returns>An anonymous projection with payloadType, rtp, rtcp counts.</returns>
    public object TrySnapshot(string cameraPath, string sessionId)
    {
        var grouped = _stats.Where(k => k.Key.path.Equals(cameraPath, StringComparison.OrdinalIgnoreCase) && k.Key.session == sessionId)
            .GroupBy(k => k.Key.payload)
            .Select(g => new { payloadType = g.Key, rtp = g.Sum(x => Interlocked.Read(ref x.Value.Rtp)), rtcp = g.Sum(x => Interlocked.Read(ref x.Value.Rtcp)) })
            .ToList();
        return grouped;
    }

    /// <summary>
    /// Captures aggregated counters for all cameras and sessions in a nested dictionary suitable for API serialization.
    /// </summary>
    public IDictionary<string, object> SnapshotAll()
    {
        Dictionary<string, object> dict = new(StringComparer.OrdinalIgnoreCase);
        foreach (var camGroup in _stats.GroupBy(k => k.Key.path, StringComparer.OrdinalIgnoreCase))
        {
            var sessions = camGroup.GroupBy(k => k.Key.session)
                .ToDictionary(sg => sg.Key, sg => (object)sg.GroupBy(pt => pt.Key.payload)
                    .Select(ptg => new { payloadType = ptg.Key, rtp = ptg.Sum(x => Interlocked.Read(ref x.Value.Rtp)), rtcp = ptg.Sum(x => Interlocked.Read(ref x.Value.Rtcp)) })
                    .ToList(), StringComparer.OrdinalIgnoreCase);
            dict[camGroup.Key] = sessions;
        }

        return dict;
    }

    private void MaybeLog(string cameraPath, string sessionId, int payloadType, DownstreamStats stats)
    {
        DateTime now = DateTime.UtcNow;
        bool shouldLog = false;
        long rtp = 0, rtcp = 0;
        lock (stats)
        {
            if (now - stats.WindowStart >= _window)
            {
                stats.WindowStart = now;
                rtp = stats.Rtp;
                rtcp = stats.Rtcp;
                stats.Rtp = 0;
                stats.Rtcp = 0;
                shouldLog = true;
            }
        }

        if (!shouldLog)
        {
            return;
        }

        Log(cameraPath, sessionId, payloadType, rtp, rtcp);
    }

    private void Log(string cameraPath, string sessionId, int payloadType, long rtp, long rtcp)
    {
        if (!logger.IsEnabled(LogLevel.Debug))
        {
            return;
        }

        string cameraId = "unknown";
        if (cameras.TryGetValue(cameraPath, out CameraStream? camera))
        {
            cameraId = camera.CameraId;
        }

        string clientShortId = sessionId.Length > 8 ? sessionId[..8] : sessionId;
        logger.LogDebug("[{CameraId}] [client-{Client}] Sent data stats: PT={PT} RTP={Rtp} RTCP={Rtcp} ({Window}s).", cameraId, clientShortId, payloadType, rtp, rtcp, (int)_window.TotalSeconds);
    }
}