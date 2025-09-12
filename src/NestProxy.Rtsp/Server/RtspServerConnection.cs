using System.Threading.Channels;

using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Transport;

namespace NestProxy.Rtsp.Server;

/// <summary>
/// Represents a server-side RTSP connection that accepts incoming requests from a client.
/// Wraps RtspConnection and provides server-specific functionality.
/// </summary>
public sealed class RtspServerConnection : IDisposable, IAsyncDisposable
{
    private readonly RtspConnection _connection;
    private bool _disposed;

    public event EventHandler? Disconnected;

    /// <summary>
    /// Initializes a new instance of the RtspServerConnection class using the specified RTSP transport and optional
    /// frame channel capacity.
    /// </summary>
    /// <param name="transport">The transport mechanism used for RTSP communication. Cannot be null.</param>
    /// <param name="frameChannelCapacity">The maximum number of frame channels to allocate for the connection. Specify 0 to use the default capacity.</param>
    public RtspServerConnection(IRtspTransport transport, int frameChannelCapacity = 0)
    {
        _connection = new RtspConnection(transport, frameChannelCapacity);
        _connection.Disconnected += OnDisconnected;
    }

    /// <summary>
    /// Gets the remote address of the connection.
    /// </summary>
    public string RemoteAddress => _connection.RemoteAddress;

    /// <summary>
    /// Gets an asynchronous reader that yields RTSP messages as they are parsed.
    /// </summary>
    public ChannelReader<RtspMessage> Messages => _connection.Messages;

    /// <summary>
    /// Starts the read loop to process incoming messages.
    /// </summary>
    public void Start()
    {
        _connection.Start();
    }

    /// <summary>
    /// Sends an RTSP response message.
    /// </summary>
    /// <param name="response">The response to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task SendResponseAsync(RtspResponse response, CancellationToken cancellationToken = default)
    {
        await _connection.SendResponseAsync(response, cancellationToken);
    }

    /// <summary>
    /// Sends interleaved data on the specified channel.
    /// </summary>
    /// <param name="channel">The channel number.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>

    public async Task SendInterleavedDataAsync(byte channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
    {
        await _connection.SendInterleavedDataAsync(channel, data, cancellationToken);
    }

    private void OnDisconnected(object? sender, EventArgs e)
    {
        Disconnected?.Invoke(this, e);
    }

    /// <summary>
    /// Releases all resources used by the current instance of the class.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection.Disconnected -= OnDisconnected;
        _connection.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _connection.Disconnected -= OnDisconnected;
        await _connection.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
