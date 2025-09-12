using System.Net;
using System.Net.Sockets;

using NestProxy.Rtsp.Transport;

namespace NestProxy.Server.Streaming.Forwarding.Binding;

/// <summary>
/// Represents a concrete downstream transport binding for a single RTP payload type (track) within
/// a <see cref="ClientSession"/>. Supports either interleaved TCP channels or dedicated UDP sockets.
/// </summary>
public sealed class TrackBinding : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Indicates whether this binding uses UDP (true) or RTSP TCP interleaving (false).
    /// </summary>
    public bool UseUdp { get; set; }

    /// <summary>
    /// Interleaved RTP/RTCP transport (present only when <see cref="UseUdp"/> is false).
    /// </summary>
    public IRtpTransport? Transport { get; set; }

    /// <summary>
    /// Interleaved channel number for RTP data (TCP mode).
    /// </summary>
    public int? InterleavedDataChannel { get; set; }

    /// <summary>
    /// Interleaved channel number for RTCP control (TCP mode).
    /// </summary>
    public int? InterleavedControlChannel { get; set; }

    /// <summary>
    /// Server UDP socket used to send RTP packets (UDP mode).
    /// </summary>
    public Socket? UdpRtpSocket { get; set; }

    /// <summary>
    /// Server UDP socket used to send RTCP packets (UDP mode).
    /// </summary>
    public Socket? UdpRtcpSocket { get; set; }

    /// <summary>
    /// Client's RTP endpoint for this track (UDP mode).
    /// </summary>
    public IPEndPoint? ClientRtpEndpoint { get; set; }

    /// <summary>
    /// Client's RTCP endpoint for this track (UDP mode).
    /// </summary>
    public IPEndPoint? ClientRtcpEndpoint { get; set; }

    /// <summary>
    /// Allocated server RTP port (UDP mode).
    /// </summary>
    public int? ServerRtpPort { get; set; }

    /// <summary>
    /// Allocated server RTCP port (UDP mode).
    /// </summary>
    public int? ServerRtcpPort { get; set; }

    /// <summary>
    /// Disposes the transport binding.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        UdpRtpSocket?.Dispose();
        UdpRtcpSocket?.Dispose();
        Transport?.Dispose();

        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        UdpRtpSocket?.Dispose();
        UdpRtcpSocket?.Dispose();

        if (Transport is IAsyncDisposable ad)
        {
            await ad.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            Transport?.Dispose();
        }

        _disposed = true;
    }
}