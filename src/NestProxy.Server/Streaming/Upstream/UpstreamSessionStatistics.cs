namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Tracks RTP and RTCP packet counts for an upstream session and periodically logs statistics.
/// </summary>
public sealed class UpstreamSessionStatistics(string cameraId, string instanceId, ILogger logger) : IUpstreamSessionStatistics
{
    private long _rtpCount;
    private long _rtcpCount;
    private long _droppedCount;
    private DateTime _lastStat = DateTime.UtcNow;

    /// <summary>
    /// Increments the RTP packet counter and logs statistics if the interval has elapsed.
    /// </summary>
    public void RecordRtp()
    {
        Interlocked.Increment(ref _rtpCount);
        MaybeLogStats();
    }

    /// <summary>
    /// Increments the RTCP packet counter and logs statistics if the interval has elapsed.
    /// </summary>
    public void RecordRtcp()
    {
        Interlocked.Increment(ref _rtcpCount);
        MaybeLogStats();
    }

    /// <summary>
    /// Increments the dropped packet counter (channel full) by the specified count and logs statistics if the interval has elapsed.
    /// </summary>
    /// <param name="count">The number of packets dropped. Must be greater than zero.</param>
    public void RecordPacketsDropped(int count)
    {
        if (count <= 0)
        {
            return;
        }

        Interlocked.Add(ref _droppedCount, count);
        MaybeLogStats();
    }

    /// <summary>
    /// Resets all counters and the last stat timestamp.
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _rtpCount, 0);
        Interlocked.Exchange(ref _rtcpCount, 0);
        Interlocked.Exchange(ref _droppedCount, 0);
        _lastStat = DateTime.UtcNow;
    }

    private void MaybeLogStats()
    {
        DateTime now = DateTime.UtcNow;
        if ((now - _lastStat) > TimeSpan.FromSeconds(10))
        {
            _lastStat = now;
            long rtp = Interlocked.Exchange(ref _rtpCount, 0);
            long rtcp = Interlocked.Exchange(ref _rtcpCount, 0);
            long dropped = Interlocked.Exchange(ref _droppedCount, 0);

            if (dropped > 0)
            {
                logger.LogWarning("[{CameraId}] [sess-{Sess}] Upstream stats: RTP={Rtp} RTCP={Rtcp} Dropped={Dropped} (10s) - Channel backpressure detected!",
                    cameraId, instanceId, rtp, rtcp, dropped);
            }
            else
            {
                logger.LogInformation("[{CameraId}] [sess-{Sess}] Upstream stats: RTP={Rtp} RTCP={Rtcp} (10s).",
                    cameraId, instanceId, rtp, rtcp);
            }
        }
    }
}
