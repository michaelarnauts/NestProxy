namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Tracks RTP and RTCP packet counts for an upstream session and periodically logs statistics.
/// </summary>
public interface IUpstreamSessionStatistics
{
    /// <summary>
    /// Increments the RTP packet counter and logs statistics if the interval has elapsed.
    /// </summary>
    void RecordRtp();

    /// <summary>
    /// Increments the RTCP packet counter and logs statistics if the interval has elapsed.
    /// </summary>
    void RecordRtcp();

    /// <summary>
    /// Increments the dropped packet counter (channel full) by the specified count and logs statistics if the interval has elapsed.
    /// </summary>
    /// <param name="count">Number of dropped packets to add.</param>
    void RecordPacketsDropped(int count);

    /// <summary>
    /// Resets all counters and the last stat timestamp.
    /// </summary>
    void Reset();
}
