using System.Text;

namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents an RTSP Transport header with parsed components.
/// </summary>
public class RtspTransport
{
    /// <summary>
    /// Gets or sets the lower transport protocol (TCP or UDP).
    /// </summary>
    public string LowerTransport { get; set; } = "TCP";

    /// <summary>
    /// Gets or sets the interleaved channel pair for TCP transport.
    /// </summary>
    public PortCouple? Interleaved { get; set; }

    /// <summary>
    /// Gets or sets the client port pair for UDP transport.
    /// </summary>
    public PortCouple? ClientPort { get; set; }

    /// <summary>
    /// Gets or sets the server port pair for UDP transport.
    /// </summary>
    public PortCouple? ServerPort { get; set; }

    /// <summary>
    /// Gets or sets the destination address for multicast.
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Gets or sets the source address.
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets the SSRC value.
    /// </summary>
    public uint? Ssrc { get; set; }

    /// <summary>
    /// Gets or sets the transport mode (PLAY or RECORD).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Gets a value indicating whether this is multicast transport.
    /// </summary>
    public bool IsMulticast => Destination is not null;

    /// <summary>
    /// Parses a Transport header value.
    /// </summary>
    public static RtspTransport Parse(string transportHeader)
    {
        var transport = new RtspTransport();
        var parts = transportHeader.Split(';');

        foreach (var part in parts)
        {
            var trimmed = part.Trim();

            if (trimmed.StartsWith("RTP/AVP/TCP", StringComparison.OrdinalIgnoreCase))
            {
                transport.LowerTransport = "TCP";
            }
            else if (trimmed.StartsWith("RTP/AVP/UDP", StringComparison.OrdinalIgnoreCase))
            {
                transport.LowerTransport = "UDP";
            }
            else if (trimmed.StartsWith("RTP/AVP", StringComparison.OrdinalIgnoreCase))
            {
                transport.LowerTransport = "UDP";
            }
            else if (trimmed.StartsWith("interleaved=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["interleaved=".Length..];
                if (PortCouple.TryParse(value, out var couple))
                {
                    transport.Interleaved = couple;
                }
            }
            else if (trimmed.StartsWith("client_port=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["client_port=".Length..];
                if (PortCouple.TryParse(value, out var couple))
                {
                    transport.ClientPort = couple;
                }
            }
            else if (trimmed.StartsWith("server_port=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["server_port=".Length..];
                if (PortCouple.TryParse(value, out var couple))
                {
                    transport.ServerPort = couple;
                }
            }
            else if (trimmed.StartsWith("destination=", StringComparison.OrdinalIgnoreCase))
            {
                transport.Destination = trimmed["destination=".Length..];
            }
            else if (trimmed.StartsWith("source=", StringComparison.OrdinalIgnoreCase))
            {
                transport.Source = trimmed["source=".Length..];
            }
            else if (trimmed.StartsWith("ssrc=", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmed["ssrc=".Length..];
                if (uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var ssrc))
                {
                    transport.Ssrc = ssrc;
                }
            }
            else if (trimmed.StartsWith("mode=", StringComparison.OrdinalIgnoreCase))
            {
                transport.Mode = trimmed["mode=".Length..].Trim('"');
            }
        }

        return transport;
    }

    /// <summary>
    /// Converts the transport to a header value string.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        if (LowerTransport.Equals("TCP", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append("RTP/AVP/TCP");
            if (Interleaved.HasValue)
            {
                sb.Append($";interleaved={Interleaved.Value}");
            }
        }
        else
        {
            sb.Append("RTP/AVP");
            if (IsMulticast)
            {
                sb.Append(";multicast");
            }
            else
            {
                sb.Append(";unicast");
            }

            if (ClientPort.HasValue)
            {
                sb.Append($";client_port={ClientPort.Value}");
            }

            if (ServerPort.HasValue)
            {
                sb.Append($";server_port={ServerPort.Value}");
            }

            if (Destination is not null)
            {
                sb.Append($";destination={Destination}");
            }

            if (Source is not null)
            {
                sb.Append($";source={Source}");
            }
        }

        if (Ssrc.HasValue)
        {
            sb.Append($";ssrc={Ssrc.Value:X8}");
        }

        if (Mode is not null)
        {
            sb.Append($";mode=\"{Mode}\"");
        }

        return sb.ToString();
    }
}
