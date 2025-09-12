using System.Net.Sockets;

using Polly;
using Polly.Contrib.WaitAndRetry;
using Polly.Extensions.Http;
using Polly.Retry;

namespace NestProxy.Server.Core;

/// <summary>
/// Centralized resilience policies for HTTP, RTSP, and TCP operations using Polly.
/// </summary>
/// <remarks>
/// This class provides pre-configured resilience strategies for:
/// - HTTP calls to Google APIs (retry with exponential backoff + jitter)
/// - RTSP protocol operations (retry transient failures)
/// - TCP connection establishment (retry with circuit breaker)
/// All policies use decorrelated jitter backoff V2 for optimal retry behavior.
/// </remarks>
public static class ResiliencePolicies
{
    /// <summary>
    /// HTTP resilience policy for transient failures with timeout, exponential backoff and decorrelated jitter.
    /// </summary>
    /// <remarks>
    /// Uses Polly's HandleTransientHttpError which handles:
    /// - Network failures (HttpRequestException)
    /// - HTTP 5xx server errors
    /// - HTTP 408 Request Timeout
    /// </remarks>
    /// <param name="timeoutMs">Request timeout in milliseconds (default: 30000).</param>
    /// <param name="retryCount">Number of retry attempts (default: 3).</param>
    /// <param name="medianFirstRetryDelayMs">Median delay for first retry in milliseconds (default: 1000).</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <returns>Configured async resilience policy for HTTP operations.</returns>
    public static IAsyncPolicy<HttpResponseMessage> CreateHttpPolicy(int timeoutMs = 30000, int retryCount = 3, int medianFirstRetryDelayMs = 1000, ILogger? logger = null)
    {
        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromMilliseconds(timeoutMs));
        var delay = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromMilliseconds(medianFirstRetryDelayMs),
            retryCount: retryCount);

        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                sleepDurations: delay,
                onRetry: (outcome, delay, attempt, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var exception = outcome.Exception?.Message ?? "N/A";
                    logger?.LogWarning("HTTP retry attempt {Attempt}/{MaxRetries} after {Delay}ms delay. Status: {Status}, Exception: {Exception}",
                            attempt, retryCount, delay.TotalMilliseconds, statusCode, exception);
                });

        return Policy.WrapAsync(retryPolicy, timeoutPolicy);
    }

    /// <summary>
    /// RTSP operation retry policy for transient network failures with decorrelated jitter.
    /// </summary>
    /// <param name="retryCount">Number of retry attempts (default: 3).</param>
    /// <param name="medianFirstRetryDelayMs">Median delay for first retry in milliseconds (default: 1000).</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <returns>Configured async retry policy for RTSP operations.</returns>
    public static AsyncRetryPolicy CreateRtspRetryPolicy(int retryCount = 3, int medianFirstRetryDelayMs = 1000, ILogger? logger = null)
    {
        var delay = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromMilliseconds(medianFirstRetryDelayMs),
            retryCount: retryCount);

        return Policy
            .Handle<SocketException>(ex =>
                ex.SocketErrorCode == SocketError.TimedOut ||
                ex.SocketErrorCode == SocketError.ConnectionReset ||
                ex.SocketErrorCode == SocketError.ConnectionAborted)
            .Or<IOException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(
                sleepDurations: delay,
                onRetry: (exception, delay, attempt, context) =>
                {
                    logger?.LogWarning(exception, "RTSP retry attempt {Attempt}/{MaxRetries} after {Delay}ms delay",
                            attempt, retryCount, delay.TotalMilliseconds);
                });
    }

    /// <summary>
    /// Infinite retry policy with continuous retry and decorrelated jitter backoff (capped).
    /// </summary>
    /// <param name="medianFirstRetryDelayMs">Median delay for first retry in milliseconds (default: 1000).</param>
    /// <param name="logger">Optional logger for retry events.</param>
    /// <returns>Configured async infinite retry policy.</returns>
    /// <remarks>
    /// Handles all exceptions except OperationCanceledException and its derived types (e.g., TaskCanceledException).
    /// </remarks>
    public static AsyncRetryPolicy CreateInfiniteRetryPolicy(int medianFirstRetryDelayMs = 1000, ILogger? logger = null)
    {
        // Use DecorrelatedJitterBackoffV2 formula manually for infinite retries with cap.
        var delay = Backoff.DecorrelatedJitterBackoffV2(
            medianFirstRetryDelay: TimeSpan.FromMilliseconds(medianFirstRetryDelayMs),
            retryCount: 10); // Cap at 10 for backoff calculation.

        return Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryForeverAsync(
                sleepDurationProvider: retryAttempt =>
                {
                    return delay.Skip(Math.Min(retryAttempt - 1, 9)).FirstOrDefault();
                },
                onRetry: (exception, attempt, delay) =>
                {
                    logger?.LogWarning(exception, "Infinite retry attempt {Attempt} after {Delay}ms delay",
                            attempt, delay.TotalMilliseconds);
                });
    }
}
