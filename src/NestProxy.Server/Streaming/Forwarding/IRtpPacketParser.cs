using System;

namespace NestProxy.Server.Streaming.Forwarding;

/// <summary>
/// Interface for parsing and validating RTP and RTCP packets.
/// </summary>
public interface IRtpPacketParser
{
    /// <summary>
    /// Determines if the given packet is a valid RTP packet.
    /// </summary>
    /// <param name="packet">The packet bytes.</param>
    /// <returns>True if valid RTP, otherwise false.</returns>
    bool IsValidRtp(ReadOnlySpan<byte> packet);

    /// <summary>
    /// Determines if the given packet is a valid RTCP packet.
    /// </summary>
    /// <param name="packet">The packet bytes.</param>
    /// <returns>True if valid RTCP, otherwise false.</returns>
    bool IsValidRtcp(ReadOnlySpan<byte> packet);

    /// <summary>
    /// Extracts the SSRC from the packet.
    /// </summary>
    /// <param name="packet">The packet bytes.</param>
    /// <param name="isRtp">True if RTP, false if RTCP.</param>
    /// <returns>The SSRC if present, otherwise null.</returns>
    uint? GetSsrc(ReadOnlySpan<byte> packet, bool isRtp);

    /// <summary>
    /// Extracts the payload type from an RTP packet.
    /// </summary>
    /// <param name="packet">The packet bytes.</param>
    /// <returns>The payload type if present, otherwise null.</returns>
    int? GetPayloadType(ReadOnlySpan<byte> packet);

    /// <summary>
    /// Extracts the RTCP packet type from an RTCP packet.
    /// </summary>
    /// <param name="packet">The packet bytes.</param>
    /// <returns>The RTCP packet type if present, otherwise null.</returns>
    byte? GetRtcpPacketType(ReadOnlySpan<byte> packet);
}
