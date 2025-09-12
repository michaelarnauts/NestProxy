using System.Text;

namespace NestProxy.Server.Streaming.Downstream.Server.Describe;

/// <summary>
/// Rewrites SDP lines for downstream clients, neutralizing ports and connection lines for local relay.
/// </summary>
public sealed class SdpRewriter : ISdpRewriter
{
    /// <inheritdoc />
    public string Rewrite(IEnumerable<string>? lines, bool isVideo, bool isAudio)
    {
        if (lines == null)
        {
            return string.Empty;
        }

        StringBuilder sb = new();
        foreach (string line in lines)
        {
            if ((isVideo && line.StartsWith("m=video")) || (isAudio && line.StartsWith("m=audio")))
            {
                string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3)
                {
                    parts[1] = "0"; // zero port
                    sb.AppendLine(string.Join(' ', parts));
                    continue;
                }
            }

            if (line.StartsWith("c=IN "))
            {
                sb.AppendLine("c=IN IP4 0.0.0.0");
                continue;
            }

            sb.AppendLine(line);
        }

        return sb.ToString();
    }
}
