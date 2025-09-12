using System.Net;
using System.Net.Sockets;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Transport;

namespace NestProxy.Rtsp.Server;

/// <summary>
/// RTSP server that listens for incoming connections and creates RtspServerConnection instances.
/// </summary>
public sealed class RtspServer : IDisposable, IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly Dictionary<string, Func<RtspRequest, CancellationToken, Task<RtspResponse>>> _handlers;
    private CancellationTokenSource? _cts;
    private Task? _acceptTask;

    public event EventHandler<RtspServerConnection>? ClientConnected;

    /// <summary>
    /// Gets the port the server is listening on.
    /// </summary>
    public int Port { get; private set; }

    /// <summary>
    /// Gets whether the server is currently running.
    /// </summary>
    public bool IsRunning => _acceptTask != null && !_acceptTask.IsCompleted;

    public RtspServer(int port)
    {
        Port = port;
        _listener = new TcpListener(IPAddress.Any, port);
        _handlers = [];
    }

    /// <summary>
    /// Registers a handler for the specified RTSP method.
    /// </summary>
    /// <param name="method">The RTSP method to handle (e.g., RtspMethod.OPTIONS).</param>
    /// <param name="handler">The handler function.</param>
    public void RegisterHandler(string method, Func<RtspRequest, CancellationToken, Task<RtspResponse>> handler)
    {
        _handlers[method] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    /// <summary>
    /// Starts the server asynchronously.
    /// </summary>
    public async Task StartAsync()
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Server is already running");
        }

        _listener.Start();

        // Update Port property if using dynamic port allocation (port 0)
        if (Port == 0 && _listener.LocalEndpoint is IPEndPoint endpoint)
        {
            Port = endpoint.Port;
        }

        _cts = new CancellationTokenSource();
        _acceptTask = AcceptLoopAsync(_cts.Token);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the server asynchronously.
    /// </summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();

        if (_acceptTask != null)
        {
            try
            {
                await _acceptTask.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore timeout
            }
        }

        _listener.Stop();
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                RtspTcpTransport transport = new(client);
                RtspServerConnection connection = new(transport);

                ClientConnected?.Invoke(this, connection);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                // Log and continue
            }
        }
    }

    /// <summary>
    /// Releases all resources used by the <see cref="RtspServer"/>.
    /// </summary>
    public void Dispose()
    {
        _cts?.Cancel();
        _listener.Stop();
        _cts?.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
