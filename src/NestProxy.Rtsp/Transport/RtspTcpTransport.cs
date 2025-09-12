using System.Net;
using System.Net.Sockets;
using System.Text;

using NestProxy.Rtsp.Messages;

namespace NestProxy.Rtsp.Transport;

/// <summary>
/// Custom TCP transport implementation for RTSP connections.
/// </summary>
public class RtspTcpTransport : IRtspTransport
{
    private readonly string _host;
    private readonly int _port;
    private Socket? _socket;
    private NetworkStream? _stream;
    private uint _commandIndex;
    private bool _disposed;

    /// <summary>
    /// Gets the hostname or IP address to connect to.
    /// </summary>
    protected string Host => _host;

    /// <summary>
    /// Initializes a new instance of the <see cref="RtspTcpTransport"/> class.
    /// </summary>
    /// <param name="host">The hostname or IP address to connect to.</param>
    /// <param name="port">The port number to connect to.</param>
    public RtspTcpTransport(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(port);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);

        _host = host;
        _port = port;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RtspTcpTransport"/> class from an existing TcpClient.
    /// </summary>
    /// <param name="client">The connected TcpClient.</param>
    public RtspTcpTransport(TcpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client.Client.RemoteEndPoint is IPEndPoint remoteEp)
        {
            _host = remoteEp.Address.ToString();
            _port = remoteEp.Port;
        }
        else
        {
            _host = "unknown";
            _port = 0;
        }

        _socket = client.Client;
        _stream = client.GetStream();
    }

    /// <inheritdoc />
    public bool Connected => _socket?.Connected ?? false;

    /// <inheritdoc />
    public IPEndPoint RemoteEndPoint => _socket?.RemoteEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.None, 0);

    /// <inheritdoc />
    public IPEndPoint LocalEndPoint => _socket?.LocalEndPoint as IPEndPoint ?? new IPEndPoint(IPAddress.None, 0);

    /// <inheritdoc />
    public string RemoteAddress => RemoteEndPoint.Address.ToString();

    /// <inheritdoc />
    public uint NextCommandIndex()
    {
        return ++_commandIndex;
    }

    /// <inheritdoc />
    public void Reconnect()
    {
        Close();
        Connect();
    }

    /// <summary>
    /// Establishes the TCP connection to the remote host.
    /// </summary>
    protected virtual void Connect()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true,
            SendTimeout = 5000,
            ReceiveTimeout = 30000
        };

        _socket.Connect(_host, _port);
        _stream = new NetworkStream(_socket, ownsSocket: true);
    }

    /// <inheritdoc />
    public void Close()
    {
        _stream?.Dispose();
        _stream = null;
        _socket?.Dispose();
        _socket = null;
    }

    /// <inheritdoc />
    public virtual Stream GetStream()
    {
        if (_stream is null)
        {
            Connect();
        }

        return _stream!;
    }

    /// <inheritdoc />
    public virtual Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Stream stream = GetStream();
        return stream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Stream stream = GetStream();
        return stream.WriteAsync(buffer, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    public async Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Serialize and send request
        string requestText = request.ToString();
        byte[] requestBytes = Encoding.UTF8.GetBytes(requestText);
        await WriteAsync(requestBytes, cancellationToken);

        // Receive and parse response
        var response = await ReceiveMessageAsync(cancellationToken) as RtspResponse;
        return response ?? throw new InvalidOperationException("Expected response but received request");
    }

    /// <inheritdoc />
    public async Task SendResponseAsync(RtspResponse response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        string responseText = response.ToString();
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseText);
        await WriteAsync(responseBytes, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<object> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        Stream stream = GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        // Read first line
        string? firstLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(firstLine))
        {
            throw new InvalidOperationException("Connection closed");
        }

        // Parse headers
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken)))
        {
            int colonIndex = line.IndexOf(':');
            if (colonIndex > 0)
            {
                string key = line[..colonIndex].Trim();
                string value = line[(colonIndex + 1)..].Trim();
                headers[key] = value;
            }
        }

        // Read body if Content-Length is present
        string? body = null;
        if (headers.TryGetValue("Content-Length", out string? contentLengthStr) &&
            int.TryParse(contentLengthStr, out int contentLength) &&
            contentLength > 0)
        {
            char[] buffer = new char[contentLength];
            int totalRead = 0;
            while (totalRead < contentLength)
            {
                int read = await reader.ReadAsync(buffer.AsMemory(totalRead, contentLength - totalRead), cancellationToken);
                if (read == 0)
                {
                    throw new InvalidOperationException("Connection closed while reading body");
                }

                totalRead += read;
            }

            body = new string(buffer);
        }

        // Determine if request or response
        if (firstLine.StartsWith("RTSP/", StringComparison.Ordinal))
        {
            // Response
            var parts = firstLine.Split(' ', 3);
            if (parts.Length < 3)
            {
                throw new InvalidOperationException($"Invalid response line: {firstLine}");
            }

            var version = RtspVersion.Parse(parts[0]);
            int statusCode = int.Parse(parts[1]);
            string reasonPhrase = parts[2];

            var response = new RtspResponse((RtspStatusCode)statusCode, reasonPhrase, version);
            foreach (var header in headers)
            {
                response.Headers[header.Key] = header.Value;
            }

            response.Body = body;
            return response;
        }
        else
        {
            // Request
            var parts = firstLine.Split(' ', 3);
            if (parts.Length < 3)
            {
                throw new InvalidOperationException($"Invalid request line: {firstLine}");
            }

            var method = RtspMethod.Parse(parts[0]);
            var uri = new Uri(parts[1], UriKind.RelativeOrAbsolute);
            var version = RtspVersion.Parse(parts[2]);

            var request = new RtspRequest(method, uri, version);
            foreach (var header in headers)
            {
                request.Headers[header.Key] = header.Value;
            }

            request.Body = body;
            return request;
        }
    }

    /// <inheritdoc />
    public async Task<byte> PeekByteAsync(CancellationToken cancellationToken)
    {
        Stream stream = GetStream();
        byte[] buffer = new byte[1];

        // For now, just read - a proper implementation would use buffering
        int read = await stream.ReadAsync(buffer, cancellationToken);
        if (read == 0)
        {
            throw new InvalidOperationException("Connection closed");
        }

        return buffer[0];
    }

    /// <inheritdoc />
    public async Task<InterleavedData> ReceiveInterleavedDataAsync(CancellationToken cancellationToken)
    {
        Stream stream = GetStream();
        byte[] header = new byte[4];

        // Read: $ channel length-high length-low
        await stream.ReadExactlyAsync(header, cancellationToken);

        if (header[0] != 0x24)
        {
            throw new InvalidOperationException("Expected interleaved data marker");
        }

        byte channel = header[1];
        int length = (header[2] << 8) | header[3];

        byte[] data = new byte[length];
        await stream.ReadExactlyAsync(data, cancellationToken);

        return new InterleavedData { Channel = channel, Data = data };
    }

    /// <inheritdoc />
    public async Task SendInterleavedDataAsync(byte channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken)
    {
        Stream stream = GetStream();

        // Write: $ channel length-high length-low data
        byte[] header =
        [
            0x24, // '$'
            channel,
            (byte)(data.Length >> 8),
            (byte)(data.Length & 0xFF),
        ];

        await stream.WriteAsync(header, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Releases all resources used by the <see cref="RtspTcpTransport"/>.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="RtspTcpTransport"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Close();
        }

        _disposed = true;
    }

    /// <summary>
    /// asynchronously releases the managed resources used by the <see cref="RtspTcpTransport"/>.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (_stream is not null)
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
            _stream = null;
        }

        // Socket is owned by the stream, so it is disposed when the stream is disposed.
        // Reset reference.
        _socket = null;
    }
}
