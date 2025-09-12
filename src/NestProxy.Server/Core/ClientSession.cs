using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;

namespace NestProxy.Server.Core;

/// <summary>
/// Represents a single downstream client session connected to the local RTSP server for a specific
/// camera stream. Holds transport information (interleaved TCP or UDP ports) and per-payload track
/// bindings. RTP packets are forwarded transparently without sequence/timestamp rewriting.
/// </summary>
public sealed class ClientSession : IDisposable, IAsyncDisposable
{
    private bool _disposed;

    /// <summary>
    /// Server-generated opaque session identifier returned in downstream RTSP SETUP responses.
    /// </summary>
    public string SessionId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The underlying RTSP server connection (TCP connection) associated with the client.
    /// </summary>
    public required RtspServerConnection Connection { get; init; }

    /// <summary>
    /// Primary RTP interleaved transport for legacy (non per-track) delivery or track data channels.
    /// </summary>
    public IRtpTransport? Transport { get; set; }

    /// <summary>
    /// Indicates whether the client has issued PLAY and is currently receiving media.
    /// </summary>
    public bool IsPlaying { get; set; }

    /// <summary>
    /// True when the downstream client requested UDP transport.
    /// </summary>
    public bool UseUdp { get; set; }

    /// <summary>
    /// Server-side UDP socket used for sending RTP to the client (if UDP selected).
    /// </summary>
    public Socket? UdpRtpSocket { get; set; }

    /// <summary>
    /// Server-side UDP socket used for sending RTCP to the client (if UDP selected).
    /// </summary>
    public Socket? UdpRtcpSocket { get; set; }

    /// <summary>
    /// Client's RTP endpoint (host/port) learned from the SETUP request Transport header.
    /// </summary>
    public IPEndPoint? ClientRtpEndpoint { get; set; }

    /// <summary>
    /// Client's RTCP endpoint (host/port) learned from the SETUP request Transport header.
    /// </summary>
    public IPEndPoint? ClientRtcpEndpoint { get; set; }

    /// <summary>
    /// Allocated server RTP port (even) when using UDP.
    /// </summary>
    public int? ServerRtpPort { get; set; }

    /// <summary>
    /// Allocated server RTCP port (odd) when using UDP.
    /// </summary>
    public int? ServerRtcpPort { get; set; }

    /// <summary>
    /// Server-assigned SSRC used when advertising downstream transport (hex string in SETUP).
    /// </summary>
    public uint Ssrc { get; set; } = (uint)Random.Shared.Next(int.MinValue, int.MaxValue);

    /// <summary>
    /// UTC timestamp of the last RTSP or keep-alive activity from this client.
    /// </summary>
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Per-RTP payload type list of track bindings (multiple entries if duplicate PT across tracks).
    /// </summary>
    public ConcurrentDictionary<int, List<TrackBinding>> BindingsByPayload { get; } = new();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Dispose direct transports/sockets
        UdpRtpSocket?.Dispose();
        UdpRtcpSocket?.Dispose();
        Transport?.Dispose();

        // Dispose bindings
        foreach (var list in BindingsByPayload.Values)
        {
            lock (list)
            {
                foreach (var binding in list)
                {
                    binding.Dispose();
                }
                list.Clear();
            }
        }

        BindingsByPayload.Clear();

        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        // Sockets are sync dispose
        UdpRtpSocket?.Dispose();
        UdpRtcpSocket?.Dispose();

        // Transport async dispose
        if (Transport is IAsyncDisposable ad)
        {
            await ad.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            Transport?.Dispose();
        }

        // Bindings async dispose
        foreach (var list in BindingsByPayload.Values)
        {
            List<TrackBinding> snapshot;
            lock (list)
            {
                snapshot = [.. list];
                list.Clear();
            }

            foreach (var binding in snapshot)
            {
                await binding.DisposeAsync().ConfigureAwait(false);
            }
        }

        BindingsByPayload.Clear();

        _disposed = true;
    }
}