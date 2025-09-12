namespace NestProxy.Server.Streaming.Forwarding;

/// <summary>
/// Provides methods to parse and validate RTP and RTCP packets.
/// </summary>
public sealed class RtpPacketParser : IRtpPacketParser
{
    /// <inheritdoc />
    public bool IsValidRtp(ReadOnlySpan<byte> packet)
    {
        return packet.Length >= 12;
    }

    /// <inheritdoc />
    public bool IsValidRtcp(ReadOnlySpan<byte> packet)
    {
        return packet.Length >= 8;
    }

    /// <inheritdoc />
    public uint? GetSsrc(ReadOnlySpan<byte> packet, bool isRtp)
    {
        if (isRtp && packet.Length >= 12)
        {
            // RTP: SSRC is at offset 8
            return (uint)((packet[8] << 24) | (packet[9] << 16) | (packet[10] << 8) | packet[11]);
        }
        else if (!isRtp && packet.Length >= 8)
        {
            // RTCP: SSRC is at offset 4
            return (uint)((packet[4] << 24) | (packet[5] << 16) | (packet[6] << 8) | packet[7]);
        }

        return null;
    }

    /// <inheritdoc />
    public int? GetPayloadType(ReadOnlySpan<byte> packet)
    {
        if (packet.Length >= 12)
        {
            return packet[1] & 0x7F;
        }

        return null;
    }

    /// <inheritdoc />
    public byte? GetRtcpPacketType(ReadOnlySpan<byte> packet)
    {
        if (packet.Length >= 8)
        {
            return packet[1];
        }

        return null;
    }
}
