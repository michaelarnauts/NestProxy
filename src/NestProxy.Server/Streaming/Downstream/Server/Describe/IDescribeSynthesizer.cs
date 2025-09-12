namespace NestProxy.Server.Streaming.Downstream.Server.Describe;

/// <summary>
/// Builds a synthesized downstream SDP for a registered camera, neutralizing connection addresses and
/// zeroing media ports while merging upstream video/audio media lines.
/// </summary>
public interface IDescribeSynthesizer
{
    /// <summary>
    /// Generates the SDP text for the specified camera stream, or null if the stream is not registered or has no SDP.
    /// </summary>
    string? Build(CameraStream camera);
}