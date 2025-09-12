using System.Text;

namespace NestProxy.Server.Streaming.Downstream.Server.Describe;

/// <summary>
/// Synthesizes a downstream SDP for a given camera, neutralizing ports and connection lines for local relay.
/// </summary>
public sealed class DescribeSynthesizer(ISdpRewriter sdpRewriter) : IDescribeSynthesizer
{
    /// <inheritdoc />
    public string? Build(CameraStream camera)
    {
        if (camera.Sdp == null && camera.UpstreamVideoMediaLines == null && camera.UpstreamAudioMediaLines == null)
        {
            return null;
        }

        StringBuilder sb = new();
        sb.Append(sdpRewriter.Rewrite(camera.Sdp, false, false));
        sb.Append(sdpRewriter.Rewrite(camera.UpstreamVideoMediaLines, true, false));
        sb.Append(sdpRewriter.Rewrite(camera.UpstreamAudioMediaLines, false, true));
        return sb.ToString();
    }
}