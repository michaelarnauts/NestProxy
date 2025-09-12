namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching;

/// <summary>
/// Resolves SDP control attribute values (e.g., trackID=1, absolute control URIs) to their corresponding
/// RTP payload type numbers for faster lookup during SETUP handling.
/// </summary>
public interface IControlMapResolver
{
    /// <summary>
    /// Resolves a control path to its corresponding payload type.
    /// </summary>
    /// <param name="cameraPath">The camera path (used to look up the camera stream).</param>
    /// <param name="control">The control attribute value from the SETUP request URI.</param>
    /// <returns>
    /// The RTP payload type if resolved; otherwise, null.
    /// </returns>
    int? Resolve(string cameraPath, string control);
}
