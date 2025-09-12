using System.Net;

using NestProxy.Rtsp.Messages;

namespace NestProxy.Rtsp.Transport;

/// <summary>
/// Defines the contract for RTSP transport implementations.
/// </summary>
public interface IRtspTransport : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets a value indicating whether the transport is currently connected.
    /// </summary>
    bool Connected { get; }

    /// <summary>
    /// Gets the remote endpoint of the connection.
    /// </summary>
    IPEndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Gets the local endpoint of the connection.
    /// </summary>
    IPEndPoint LocalEndPoint { get; }

    /// <summary>
    /// Gets the remote address as a string.
    /// </summary>
    string RemoteAddress { get; }

    /// <summary>
    /// Gets the next command index (CSeq) for RTSP requests.
    /// </summary>
    /// <returns>The next command sequence number.</returns>
    uint NextCommandIndex();

    /// <summary>
    /// Reconnects the transport by closing and re-establishing the connection.
    /// </summary>
    void Reconnect();

    /// <summary>
    /// Closes the connection.
    /// </summary>
    void Close();

    /// <summary>
    /// Gets the underlying stream for reading and writing data.
    /// </summary>
    /// <returns>The network stream.</returns>
    Stream GetStream();

    /// <summary>
    /// Writes data to the transport asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer containing data to write.</param>
    /// <param name="offset">The offset in the buffer to start writing from.</param>
    /// <param name="count">The number of bytes to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Writes data to the transport asynchronously.
    /// </summary>
    /// <param name="buffer">The buffer containing data to write.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an RTSP request and waits for the response.
    /// </summary>
    /// <param name="request">The request to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The RTSP response.</returns>
    Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Sends an RTSP response.
    /// </summary>
    /// <param name="response">The response to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendResponseAsync(RtspResponse response, CancellationToken cancellationToken);

    /// <summary>
    /// Receives an RTSP message (request or response).
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The received RTSP message.</returns>
    Task<object> ReceiveMessageAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Peeks at the next byte without consuming it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The next byte.</returns>
    Task<byte> PeekByteAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Receives interleaved binary data.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The interleaved data.</returns>
    Task<InterleavedData> ReceiveInterleavedDataAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sends interleaved binary data.
    /// </summary>
    /// <param name="channel">The channel number.</param>
    /// <param name="data">The data to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task SendInterleavedDataAsync(byte channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}
