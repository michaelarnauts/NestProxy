namespace NestProxy.Server.Streaming.Downstream.Server.Describe;

/// <summary>
/// Builds or rebuilds the mapping of SDP control attribute values to payload types for a camera stream.
/// Extracted from LocalRtspServer to reduce its responsibility surface.
/// </summary>
public interface IControlMapBuilder
{
    /// <summary>
    /// Recomputes the <see cref="CameraStream.ControlToPayloadMap"/> from the stream's current SDP.
    /// </summary>
    void Rebuild(CameraStream cam);
}