using System.Net.Sockets;

using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;

using Polly.Retry;

namespace NestProxy.Server.Streaming.Downstream.Server;

/// <summary>
/// Dedicated hosted service that owns the TCP listen socket and accepts downstream RTSP clients,
/// wiring message events to the provided callback.
/// </summary>
public sealed class ListenerAcceptLoop(IOptions<StreamingOptions> options, ILogger<ListenerAcceptLoop> logger) 
    : BackgroundService, IListenerManager, IAsyncDisposable
{
    private readonly AsyncRetryPolicy _retryPolicy = ResiliencePolicies.CreateInfiniteRetryPolicy();
    private readonly TcpListener _listener = TcpListener.Create(options.Value.RtspBasePort);

    private Func<RtspServerConnection, Task>? _onAccepted;

    /// <summary>
    /// Main accept loop for incoming RTSP clients. Accepts, starts, and tracks connections.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token for graceful shutdown.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Accept loop listening on port {Port} with resilience policies", options.Value.RtspBasePort);

        _listener.Start();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await _retryPolicy.ExecuteAsync(async (ct) =>
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(ct);
                    RtspTcpTransport transport = new(client);
                    int frameCapacity = options.Value.PacketChannelCapacity;
                    RtspServerConnection connection = new(transport, frameCapacity);

                    // Ownership transferred to callback recipient (e.g. LocalRtspServer)
                    if (_onAccepted != null)
                    {
                        await _onAccepted.Invoke(connection);
                    }
                    else
                    {
                        // No handler registered, dispose immediately to avoid leak
                        await connection.DisposeAsync();
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }

        logger.LogInformation("Accept loop stopped");
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping accept loop");

        // Stop listening for new connections
        Safe.Invoke(() => _listener?.Stop(), logger, "stopping listen socket");

        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void RegisterOnAccepted(Func<RtspServerConnection, Task> onAccepted)
    {
        _onAccepted = onAccepted;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        // Stop listening
        Safe.Invoke(() => _listener?.Stop(), logger, "stop listener in Dispose");
        
        base.Dispose();
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Stop listening
        Safe.Invoke(() => _listener?.Stop(), logger, "stop listener in DisposeAsync");
        
        // Perform base disposal (cancellation of token)
        base.Dispose();
        
        await ValueTask.CompletedTask;
    }
}