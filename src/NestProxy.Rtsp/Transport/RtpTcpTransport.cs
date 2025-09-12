using NestProxy.Rtsp.Server;

namespace NestProxy.Rtsp.Transport;

/// <summary>
/// RTP/RTCP transport over TCP using RTSP interleaved frames ($).
/// </summary>
public sealed class RtpTcpTransport : IRtpTransport
{
    private bool _disposed;

    /// <summary>
    /// Channel number for RTP data.
    /// </summary>
    public int DataChannel { get; set; }

    /// <summary>
    /// Channel number for RTCP control.
    /// </summary>
    public int ControlChannel { get; set; }

    /// <summary>
    /// RTSP server connection to use for sending frames.
    /// </summary>
    public RtspServerConnection? Connection { get; set; }

    /// <inheritdoc />
    public async Task SendFrameAsync(int channel, ReadOnlyMemory<byte> frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RtpTcpTransport));

        if (Connection == null)
        {
            throw new InvalidOperationException("Connection is not set.");
        }

        await Connection.SendInterleavedDataAsync((byte)channel, frame, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteToDataPortAsync(ReadOnlyMemory<byte> packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RtpTcpTransport));

        if (Connection == null)
        {
            throw new InvalidOperationException("Connection is not set.");
        }

        await Connection.SendInterleavedDataAsync((byte)DataChannel, packet).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task WriteToControlPortAsync(ReadOnlyMemory<byte> packet)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(RtpTcpTransport));

        if (Connection == null)
        {
            throw new InvalidOperationException("Connection is not set.");
        }

        await Connection.SendInterleavedDataAsync((byte)ControlChannel, packet).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
        
        return ValueTask.CompletedTask;
    }
}
