using Microsoft.Extensions.Options;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

namespace NestProxy.Server.Streaming.Downstream.Server;

/// <summary>
/// In-process RTSP server that exposes locally synthesized RTSP sessions for upstream Google Nest camera
/// streams. It accepts downstream client connections, serves SDP (rebuilt from upstream DESCRIBE), handles
/// SETUP/PLAY/TEARDOWN, manages UDP or interleaved transports, and forwards RTP/RTCP packets as a transparent
/// RTP translator (RFC 3550 §7.1) without rewriting RTP sequence numbers or timestamps. Binding selection
/// leverages payload type and (optionally) SSRC heuristics but original RTP headers are preserved end-to-end.
/// </summary>
public class LocalRtspServer(
    ILogger<LocalRtspServer> logger,
    IOptions<StreamingOptions> options,
    IStatisticsReporter statisticsReporter,
    ISessionRegistry sessionManager,
    CameraRegistry cameraRegistry,
    IRtspRequestDispatcher dispatcher,
    IControlMapBuilder controlMapBuilder,
    IStreamForwarder forwarder,
    IListenerManager listenerManager) : BackgroundService, ILocalRtspServer, IAsyncDisposable
{
    private readonly List<RtspServerConnection> _connections = [];
    private CancellationTokenSource? _stopCts;
    private Task? _sweeper;
    private readonly HashSet<Task> _connectionTasks = [];

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Register callback for accepted listeners.
            listenerManager.RegisterOnAccepted(OnAcceptedAsync);

            // Register callback for connection removal.
            dispatcher.RegisterOnRemoveConnection(OnRemoveConnectionAsync);

            _stopCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            _sweeper = Task.Run(() => SessionSweepLoop(_stopCts.Token), stoppingToken);

            logger.LogInformation("Local RTSP server session management active on port {Port} (listener handled by ListenerAcceptLoop).", options.Value.RtspBasePort);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to start LocalRtspServer - server will not be available");
            throw; // Allow host to handle critical startup failure.
        }
    }

    private async Task OnAcceptedAsync(RtspServerConnection connection)
    {
        try
        {
            lock (_connections)
            {
                _connections.Add(connection);
            }

            connection.Start();

            if (_stopCts != null)
            {
                Task loop = Task.Run(async () => await ProcessRequestsAsync(connection, _stopCts.Token));
                lock (_connectionTasks)
                {
                    _connectionTasks.Add(loop);
                }

                // Self-cleanup task from the tracking set upon completion
                _ = loop.ContinueWith(t =>
                {
                    lock (_connectionTasks)
                    {
                        _connectionTasks.Remove(t);
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize accepted connection");
            Safe.Invoke(() => connection.Dispose(), logger, "connection disposal after accept failure");
        }

        await Task.CompletedTask;
    }

    private async Task ProcessRequestsAsync(RtspServerConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (RtspMessage message in connection.Messages.ReadAllAsync(cancellationToken))
            {
                if (message is not RtspRequest request)
                {
                    logger.LogDebug("Ignoring non-request message.");
                    return;
                }

                try
                {
                    await dispatcher.DispatchAsync(connection, request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "RTSP request dispatch error");
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await OnRemoveConnectionAsync(connection).ConfigureAwait(false);
        }
    }

    private async ValueTask SessionSweepLoop(CancellationToken ct)
    {
        logger.LogDebug("Session sweep loop starting");

        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(15));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    await sessionManager.SweepExpiredSessionsAsync();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Session sweep error - will retry on next interval");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Session sweep loop unexpected error");
        }
    }

    /// <summary>
    /// Registers a new camera or updates an existing registration (payload types / SDP lines / control map).
    /// Safe to call repeatedly as upstream SDP changes (e.g. upon token refresh or capability change).
    /// </summary>
    /// <param name="stream">The camera stream metadata to register.</param>
    public void RegisterOrUpdateCamera(CameraStream stream)
    {
        if (!cameraRegistry.TryGetValue(stream.CameraId, out CameraStream? existing))
        {
            // New camera - add to registry
            cameraRegistry.AddOrUpdate(stream.CameraId, stream);
            logger.LogInformation("Registered new camera: {CameraId}", stream.CameraId);
        }
        else
        {
            // Existing camera - update mutable properties without replacing the object
            // This preserves the Sessions list and other downstream client state
            existing.Sdp = stream.Sdp;
            existing.VideoPayloadType = stream.VideoPayloadType;
            existing.AudioPayloadType = stream.AudioPayloadType;
            existing.UpstreamVideoMediaLines = stream.UpstreamVideoMediaLines;
            existing.UpstreamAudioMediaLines = stream.UpstreamAudioMediaLines;
            logger.LogDebug("Updated camera registration: {CameraId}", stream.CameraId);
        }

        if (stream.Sdp != null)
        {
            // Use existing camera for control map rebuild if updating
            CameraStream target = cameraRegistry.TryGetValue(stream.CameraId, out CameraStream? cam) ? cam : stream;
            controlMapBuilder.Rebuild(target);
        }
    }

    /// <inheritdoc />
    public async ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default)
    {
        try
        {
            // SAFETY: Extract payload type before async call to avoid accessing buffer after it may be reused.
            int payloadType = rtpPacket.Length >= 2 ? rtpPacket.Span[1] & 0x7F : -1;

            await forwarder.ForwardRtpAsync(cameraPath, rtpPacket, cancellationToken);

            if (payloadType >= 0 && cameraRegistry.TryGetValue(cameraPath, out CameraStream? cam))
            {
                foreach (ClientSession session in cam.Sessions)
                {
                    if (!string.IsNullOrEmpty(session.SessionId))
                    {
                        statisticsReporter.Increment(cameraPath, session.SessionId, payloadType, true);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Error forwarding RTP packet for {CameraPath} - packet dropped", cameraPath);
        }
    }

    /// <inheritdoc />
    public async ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default)
    {
        try
        {
            // Extract payload type before async call to avoid accessing buffer after it may be reused.
            int payloadType = rtcpPacket.Length >= 2 ? rtcpPacket.Span[1] & 0xFF : -1;

            await forwarder.ForwardRtcpAsync(cameraPath, rtcpPacket, cancellationToken);

            if (payloadType >= 0 && cameraRegistry.TryGetValue(cameraPath, out CameraStream? cam))
            {
                foreach (ClientSession session in cam.Sessions)
                {
                    if (!string.IsNullOrEmpty(session.SessionId))
                    {
                        statisticsReporter.Increment(cameraPath, session.SessionId, payloadType, false);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Error forwarding RTCP packet for {CameraPath} - packet dropped", cameraPath);
        }
    }

    /// <inheritdoc />
    public string BuildPublicUrl(string host, int port, string path) => $"rtsp://{host}:{port}/{path}";

    private async Task OnRemoveConnectionAsync(RtspServerConnection connection)
    {
        lock (_connections)
        {
            _connections.Remove(connection);
        }

        await sessionManager.RemoveSessionsForConnectionAsync(connection);

        await Safe.InvokeAsync(async () => await connection.DisposeAsync(), logger, "connection disposal");
    }

    /// <summary>
    /// Stops the RTSP server, closing the listen socket and disposing all active connections and their sessions.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping LocalRtspServer with {Count} active connections", _connections.Count);

        Safe.Invoke(() => _stopCts?.Cancel(), logger, "cancellation token source");

        List<RtspServerConnection> copy;
        lock (_connections)
        {
            copy = [.. _connections];
        }

        foreach (RtspServerConnection connection in copy)
        {
            await Safe.InvokeAsync(async () => await OnRemoveConnectionAsync(connection), logger, $"removing connection {connection.GetHashCode()}");
        }

        Task[] loops;
        lock (_connectionTasks)
        {
            loops = _connectionTasks.ToArray();
        }

        await Task.WhenAll(loops);

        if (_sweeper != null)
        {
            await Safe.InvokeAsync(async () => await _sweeper, logger, "sweeper task completion");
        }

        await base.StopAsync(cancellationToken);

        logger.LogInformation("LocalRtspServer stopped");
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Dispose(true);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        base.Dispose();
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes the server resources.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Safe.Invoke(() => _stopCts?.Cancel(), logger, "token cancellation");
            Safe.Invoke(() => _stopCts?.Dispose(), logger, "token disposal");
            
            // Dispose connections
            lock (_connections)
            {
                foreach (var connection in _connections)
                {
                    Safe.Invoke(connection.Dispose, logger, "connection disposal");
                }
                _connections.Clear();
            }
        }
    }

    /// <summary>
    /// Asynchronously disposes the server resources.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        Safe.Invoke(() => _stopCts?.Cancel(), logger, "token cancellation");
        
        // Wait for sweeper task if running
        if (_sweeper != null)
        {
            await Safe.InvokeAsync(async () => await _sweeper, logger, "sweeper await");
        }

        // Wait for connection loops
        Task[] loops;
        lock (_connectionTasks)
        {
            loops = _connectionTasks.ToArray();
            _connectionTasks.Clear();
        }
        await Task.WhenAll(loops);

        // Async dispose connections
        List<RtspServerConnection> conns;
        lock (_connections)
        {
            conns = new List<RtspServerConnection>(_connections);
            _connections.Clear();
        }

        foreach (var connection in conns)
        {
            await Safe.InvokeAsync(async () => await connection.DisposeAsync(), logger, "connection async disposal");
        }

        Safe.Invoke(() => _stopCts?.Dispose(), logger, "token disposal");
    }
}