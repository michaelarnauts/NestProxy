namespace NestProxy.Server.Streaming.Downstream.Server.Dispatching;

/// <summary>
/// Resolves SDP control paths to RTP payload types using the pre-parsed control map maintained
/// by <see cref="CameraStream"/> instances.
/// </summary>
/// <param name="cameras">Registry of registered camera streams.</param>
public sealed class ControlMapResolver(CameraRegistry cameras) : IControlMapResolver
{
    /// <inheritdoc />
    public int? Resolve(string cameraPath, string control)
    {
        if (string.IsNullOrWhiteSpace(control) || !cameras.TryGetValue(cameraPath, out CameraStream? camera))
        {
            return null;
        }

        if (camera.ControlToPayloadMap.TryGetValue(control, out int pt))
        {
            return pt;
        }

        if (control.StartsWith("trackID=", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(control.AsSpan(8), out int idx) &&
            camera.ControlToPayloadMap.Count > 0)
        {
            var ordered = camera.ControlToPayloadMap
                .Where(kv => kv.Key.StartsWith("trackID=", StringComparison.OrdinalIgnoreCase))
                .OrderBy(kv => kv.Key)
                .ToList();

            if (idx >= 0 && idx < ordered.Count)
            {
                return ordered[idx].Value;
            }

            if (idx - 1 >= 0 && idx - 1 < ordered.Count)
            {
                return ordered[idx - 1].Value;
            }
        }

        return null;
    }
}
