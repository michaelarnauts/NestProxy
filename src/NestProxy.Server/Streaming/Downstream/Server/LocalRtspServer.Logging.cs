using Microsoft.Extensions.Logging;

namespace NestProxy.Server.Services;

/// <summary>
/// Logging helpers for LocalRtspServer and related downstream server operations.
/// </summary>
internal static partial class LocalRtspServerLog
{
    /// <summary>
    /// Logs a failed RTP write and session removal.
    /// </summary>
    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "{Prefix} RTP write failed. Removing session.")]
    public static partial void RtpWriteFailed(this ILogger logger, string Prefix, Exception ex);

    /// <summary>
    /// Logs a failed RTCP write and session removal.
    /// </summary>
    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug, Message = "{Prefix} RTCP write failed. Removing session.")]
    public static partial void RtcpWriteFailed(this ILogger logger, string Prefix, Exception ex);

    /// <summary>
    /// Logs an error during session sweeping.
    /// </summary>
    [LoggerMessage(EventId = 2003, Level = LogLevel.Debug, Message = "Session sweeper error.")]
    public static partial void SessionSweeperError(this ILogger logger, Exception ex);

    /// <summary>
    /// Logs an error accepting an RTSP connection.
    /// </summary>
    [LoggerMessage(EventId = 2004, Level = LogLevel.Warning, Message = "RTSP accept error.")]
    public static partial void AcceptError(this ILogger logger, Exception ex);
}