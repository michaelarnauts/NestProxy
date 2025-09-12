namespace NestProxy.Server.Utilities;

/// <summary>
/// Provides extension methods for safely invoking delegates by suppressing exceptions that
/// may occur during execution.
/// </summary>
/// <remarks>
/// <para><strong>INTENDED USAGE - Use Safe utilities ONLY for:</strong></para>
/// <list type="bullet">
/// <item><description>Disposal and cleanup operations (IDisposable.Dispose, IAsyncDisposable.DisposeAsync)</description></item>
/// <item><description>Shutdown and cancellation sequences</description></item>
/// <item><description>Non-critical keep-alive operations</description></item>
/// <item><description>Resource cleanup where failure should not cascade</description></item>
/// </list>
/// <para><strong>DO NOT USE Safe utilities for:</strong></para>
/// <list type="bullet">
/// <item><description>Business logic operations that require error visibility</description></item>
/// <item><description>Database operations where failures must be handled</description></item>
/// <item><description>Service operations where callers need to know about failures</description></item>
/// <item><description>Stream processing where data integrity matters</description></item>
/// </list>
/// </remarks>
internal static class Safe
{
    /// <summary>
    /// Invokes the specified action, suppressing any exceptions that occur during execution.
    /// </summary>
    /// <remarks>
    /// This method executes the provided action and ignores any exceptions that are thrown.
    /// Intended for disposal and cleanup operations only.
    /// </remarks>
    /// <param name="action">The action to execute.</param>
    /// <param name="logger">Optional logger to record suppressed exceptions.</param>
    /// <param name="context">Optional context description for logging.</param>
    /// <returns>True if the action completed successfully; false if an exception was caught.</returns>
    public static bool Invoke(this Action action, ILogger? logger = null, string? context = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Safe.Invoke suppressed exception{Context}", 
                context != null ? $" in {context}" : string.Empty);
            return false;
        }
    }

    /// <summary>
    /// Invokes the specified function and returns its result, or the default value of the
    /// result type if an exception occurs.
    /// </summary>
    /// <remarks>
    /// This method suppresses all exceptions thrown by the function and returns the default
    /// value for TResult in such cases. Intended for cleanup operations only.
    /// </remarks>
    /// <typeparam name="TResult">The type of the value returned by the function.</typeparam>
    /// <param name="func">The function to invoke.</param>
    /// <param name="logger">Optional logger to record suppressed exceptions.</param>
    /// <param name="context">Optional context description for logging.</param>
    /// <returns>
    /// The result of the function if it completes successfully; otherwise, the default value
    /// for type TResult if an exception is thrown.
    /// </returns>
    public static TResult? Invoke<TResult>(this Func<TResult> func, ILogger? logger = null, string? context = null)
    {
        try
        {
            return func();
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Safe.Invoke<T> suppressed exception{Context}", 
                context != null ? $" in {context}" : string.Empty);
            return default;
        }
    }

    /// <summary>
    /// Invokes the specified async function, suppressing any exceptions that occur during execution.
    /// </summary>
    /// <remarks>
    /// This method executes the provided async function and ignores any exceptions that are thrown.
    /// Intended for async disposal and cleanup operations only.
    /// </remarks>
    /// <param name="function">The async function to execute.</param>
    /// <param name="logger">Optional logger to record suppressed exceptions.</param>
    /// <param name="context">Optional context description for logging.</param>
    /// <returns>True if the function completed successfully; false if an exception was caught.</returns>
    public static async Task<bool> InvokeAsync(this Func<Task> function, ILogger? logger = null, string? context = null)
    {
        try
        {
            await function();
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Safe.InvokeAsync suppressed exception{Context}", 
                context != null ? $" in {context}" : string.Empty);
            return false;
        }
    }
}