namespace NestProxy.Server.Models;

/// <summary>
/// Modes available for sending upstream keep-alive traffic to maintain a Nest RTSP session.
/// </summary>
public enum KeepaliveMode
{
    /// <summary>Send periodic RTSP OPTIONS requests.</summary>
    Options,
    /// <summary>Send periodic RTSP GET_PARAMETER requests (fallback to OPTIONS if rejected).</summary>
    GetParameter,
    /// <summary>Send periodic RTCP Receiver Reports (RR) with SDES CNAME over interleaved RTCP channel.</summary>
    Rtcp
}