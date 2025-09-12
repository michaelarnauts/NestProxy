namespace NestProxy.Rtsp.Sdp;

/// <summary>
/// Interface for parsing SDP (Session Description Protocol) content.
/// </summary>
public interface ISdpParser
{
    /// <summary>
    /// Parses SDP content from a string.
    /// </summary>
    /// <param name="sdpContent">The SDP content to parse.</param>
    /// <returns>A parsed SdpFile object.</returns>
    SdpFile Parse(string sdpContent);

    /// <summary>
    /// Parses SDP content from a TextReader.
    /// </summary>
    /// <param name="reader">The reader containing SDP content.</param>
    /// <returns>A parsed SdpFile object.</returns>
    SdpFile Parse(TextReader reader);
}
