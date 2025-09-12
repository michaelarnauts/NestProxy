using System.Threading.Tasks;
using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Protocol;

namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Manages the RTSP protocol state machine for an upstream session.
/// </summary>
public interface IUpstreamSessionStateMachine
{
    /// <summary>
    /// RTSP Session header value once established (null until first successful SETUP).
    /// </summary>
    string? SessionId { get; }

    /// <summary>
    /// Content-Base (or Location) derived base URL resolved from DESCRIBE.
    /// </summary>
    string? BaseUrl { get; }

    /// <summary>
    /// Aggregate (session-level) URL if provided (used for PLAY / session-level SETUP fallback).
    /// </summary>
    string? AggregateUrl { get; }

    /// <summary>
    /// Ordered list of (RTP, RTCP) interleaved channel pairs assigned per track.
    /// </summary>
    IReadOnlyList<(int data, int control)> InterleavedAssignments { get; }

    /// <summary>
    /// Raised after DESCRIBE is parsed and control URLs / track URIs are resolved.
    /// </summary>
    event Action<SdpResolutionResult>? DescribeResolved;

    /// <summary>
    /// Resets the state machine to initial conditions.
    /// </summary>
    void Reset();

    /// <summary>
    /// Handles an RTSP response based on its originating request.
    /// </summary>
    /// <param name="connection">The RTSP connection to send messages through.</param>
    /// <param name="response">The response message.</param>
    /// <param name="formatter">Formatter used for logging headers/bodies.</param>
    Task HandleResponseAsync(RtspConnection connection, RtspResponse response, IRtspResponseFormatter formatter);

    /// <summary>
    /// Returns the first control (RTCP) channel number if assigned.
    /// </summary>
    /// <param name="controlChannel">The control channel number.</param>
    /// <returns>True if a control channel is assigned; otherwise false.</returns>
    bool TryGetFirstControlChannel(out int controlChannel);
}
