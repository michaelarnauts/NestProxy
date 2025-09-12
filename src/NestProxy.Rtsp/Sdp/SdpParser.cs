namespace NestProxy.Rtsp.Sdp;

/// <summary>
/// Parses SDP (Session Description Protocol) content according to RFC 4566.
/// Implements lenient parsing to handle variations in real-world SDP.
/// </summary>
public sealed class SdpParser : ISdpParser
{
    /// <inheritdoc />
    public SdpFile Parse(string sdpContent)
    {
        using var reader = new StringReader(sdpContent);
        return Parse(reader);
    }

    /// <inheritdoc />
    public SdpFile Parse(TextReader reader)
    {
        int version = 0;
        string? origin = null;
        string? sessionName = null;
        string? sessionDescription = null;
        string? connection = null;
        string? timing = null;
        var sessionAttributes = new List<SdpAttribute>();
        var medias = new List<SdpMedia>();
        
        SdpMedia? currentMedia = null;
        bool inMediaSection = false;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            
            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // SDP lines are in format: type=value
            if (line.Length < 2 || line[1] != '=')
            {
                continue; // Invalid line, skip
            }

            var type = line[0];
            var value = line.Substring(2);

            // Check if we're starting a media section
            if (type == 'm')
            {
                // Save previous media if exists
                if (currentMedia is not null)
                {
                    medias.Add(currentMedia);
                }

                // Parse new media line
                currentMedia = ParseMediaLine(value);
                inMediaSection = true;
                continue;
            }

            // Parse line based on whether we're in session or media section
            if (!inMediaSection)
            {
                // Session-level parsing
                switch (type)
                {
                    case 'v': // Version
                        int.TryParse(value, out version);
                        break;
                    case 'o': // Origin
                        origin = value;
                        break;
                    case 's': // Session name
                        sessionName = value;
                        break;
                    case 'i': // Session description
                        sessionDescription = value;
                        break;
                    case 'c': // Connection
                        connection = value;
                        break;
                    case 't': // Timing
                        timing = value;
                        break;
                    case 'a': // Attribute
                        sessionAttributes.Add(ParseAttribute(value));
                        break;
                }
            }
            else if (currentMedia is not null)
            {
                ParseMediaSectionLine(currentMedia, type, value);
            }
        }

        // Add final media section if exists
        if (currentMedia is not null)
        {
            medias.Add(currentMedia);
        }

        return new SdpFile
        {
            Version = version,
            Origin = origin,
            SessionName = sessionName,
            SessionDescription = sessionDescription,
            Connection = connection,
            Timing = timing,
            Attributes = sessionAttributes,
            Medias = medias
        };
    }

    private void ParseSessionLine(SdpFile sdp, char type, string value)
    {
        switch (type)
        {
            case 'a': // Attribute
                var attr = ParseAttribute(value);
                sdp.Attributes.Add(attr);
                break;

            // Other fields are handled during construction below
        }
    }

    private void ParseMediaSectionLine(SdpMedia media, char type, string value)
    {
        switch (type)
        {
            case 'a': // Attribute (media-level)
                var attr = ParseAttribute(value);
                media.Attributes.Add(attr);
                break;

            case 'b': // Bandwidth
            case 'k': // Encryption key
                // Parse but don't store for now
                break;

            // Ignore other fields
        }
    }

    private static SdpMedia ParseMediaLine(string value)
    {
        // m=<media> <port> <proto> <fmt> ...
        // Example: m=video 0 RTP/AVP 96
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length < 4)
        {
            // Invalid media line, return minimal media
            return new SdpMedia
            {
                MediaType = MediaType.Unknown,
                Port = 0,
                Protocol = string.Empty
            };
        }

        var mediaType = ParseMediaType(parts[0]);
        var port = int.TryParse(parts[1], out var p) ? p : 0;
        var protocol = parts[2];
        var formats = parts.Skip(3).ToList();

        return new SdpMedia
        {
            MediaType = mediaType,
            Port = port,
            Protocol = protocol,
            Formats = formats
        };
    }

    private static MediaType ParseMediaType(string mediaString)
    {
        return mediaString.ToLowerInvariant() switch
        {
            "audio" => MediaType.Audio,
            "video" => MediaType.Video,
            "application" => MediaType.Application,
            "text" => MediaType.Text,
            _ => MediaType.Unknown
        };
    }

    private static SdpAttribute ParseAttribute(string value)
    {
        // a=<attribute> or a=<attribute>:<value>
        var colonIndex = value.IndexOf(':');
        
        if (colonIndex == -1)
        {
            // Flag attribute (no value)
            return new SdpAttribute
            {
                Key = value,
                Value = null
            };
        }

        return new SdpAttribute
        {
            Key = value.Substring(0, colonIndex),
            Value = value.Substring(colonIndex + 1)
        };
    }
}
