namespace NestProxy.Server.Models;

/// <summary>
/// Configuration options controlling behavior of the local RTSP server and upstream keep-alive strategy.
/// </summary>
public class StreamingOptions
{
    /// <summary>
    /// TCP port on which the local RTSP server listens for downstream clients.
    /// </summary>
    public int RtspBasePort { get; set; } = 8554;

    /// <summary>
    /// Enables periodic upstream keep-alives (OPTIONS / GET_PARAMETER / RTCP as configured).
    /// </summary>
    public bool KeepaliveEnabled { get; set; } = false;

    /// <summary>
    /// Mode used to send keep-alive traffic upstream when <see cref="KeepaliveEnabled"/> is true.
    /// </summary>
    public KeepaliveMode KeepaliveMode { get; set; } = KeepaliveMode.Options;

    /// <summary>
    /// Interval in seconds between upstream keep-alive messages (approximate).
    /// </summary>
    public int KeepaliveIntervalSeconds { get; set; } = 20;

    /// <summary>
    /// Idle timeout (seconds) before a downstream session is expired. Default 60.
    /// </summary>
    public int SessionTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Metrics/logging aggregation window (seconds) for downstream RTP/RTCP counters. Default 10.
    /// </summary>
    public int StatsWindowSeconds { get; set; } = 10;

    /// <summary>
    /// Time offset (seconds) before stream expiry when extension should be triggered. Default 30.
    /// </summary>
    public int StreamExtensionOffsetSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum number of packets to buffer per upstream session. Default 1000.
    /// </summary>
    public int PacketChannelCapacity { get; set; } = 1000;
}