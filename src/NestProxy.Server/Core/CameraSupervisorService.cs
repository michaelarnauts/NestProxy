using System.Collections.Concurrent;

namespace NestProxy.Server.Core;

/// <summary>
/// Periodically supervises enabled cameras, ensuring a <see cref="CameraProxyRunner"/> is active for each
/// enabled camera and stopped for disabled cameras. Acts as a lightweight scheduler, polling the database
/// at a fixed interval (default 15s).
/// </summary>
/// <remarks>
/// This service uses resilient error handling to ensure it continues operating even if individual
/// tick operations fail. Camera runners are managed independently to prevent cascading failures.
/// </remarks>
public class CameraSupervisorService(IServiceScopeFactory scopeFactory, ILogger<CameraSupervisorService> logger) : IHostedService, IDisposable
{
    private const int SupervisorIntervalSeconds = 15;
    private readonly ConcurrentDictionary<string, CameraProxyRunner> _runners = new(StringComparer.OrdinalIgnoreCase);
    private Timer? _timer;
    private bool _disposed;

    /// <summary>
    /// Starts the supervisor timer.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Camera supervisor starting with {Interval}s polling interval.", SupervisorIntervalSeconds);
        _timer = new Timer(async _ => await TickAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(SupervisorIntervalSeconds));
        return Task.CompletedTask;
    }

    private async Task TickAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var cameraRepository = scope.ServiceProvider.GetRequiredService<ICameraRepository>();

            // Ensure runners for enabled cameras.
            var enabledCameras = await cameraRepository.GetEnabledAsync();
            var enabledIds = enabledCameras.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Start runners for newly enabled cameras
            foreach (var id in enabledIds)
            {
                _runners.GetOrAdd(id, cameraId =>
                {
                    try
                    {
                        var factory = scope.ServiceProvider.GetRequiredService<ICameraProxyRunnerFactory>();
                        var runner = factory.Create(cameraId);
                        runner.Start();
                        logger.LogInformation("Started camera runner for {CameraId}", cameraId);
                        return runner;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to start camera runner for {CameraId}", cameraId);
                        throw; // Re-throw to prevent adding null runner.
                    }
                });
            }

            // Stop runners for disabled cameras.
            var disabledIds = _runners.Keys.Except(enabledIds).ToList();
            foreach (var id in disabledIds)
            {
                if (_runners.TryRemove(id, out var runner))
                {
                    try
                    {
                        await runner.StopAsync();
                        logger.LogInformation("Stopped camera runner for {CameraId}", id);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Error stopping camera runner for {CameraId}", id);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Supervisor tick error - will retry on next interval");
        }
    }

    /// <summary>
    /// Stops the supervisor and all active runners.
    /// </summary>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Camera supervisor stopping - terminating {Count} active runners", _runners.Count);

        // Stop timer first to prevent new ticks.
        if (_timer != null)
        {
            await Safe.InvokeAsync(async () => await _timer.DisposeAsync(), logger, "timer disposal");
        }

        // Stop all runners with resilient error handling.
        var stopTasks = _runners.Values.Select(async runner =>
        {
            await Safe.InvokeAsync(async () => await runner.StopAsync(), logger, $"runner {runner.CameraId} stop");
        });

        await Task.WhenAll(stopTasks);

        logger.LogInformation("Camera supervisor stopped");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases managed resources using Safe utilities to suppress disposal exceptions.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Safe.Invoke(() => _timer?.Dispose(), logger, "timer disposal");
            _timer = null;
        }

        _disposed = true;
    }
}