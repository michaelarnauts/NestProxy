namespace NestProxy.Server.Streaming.Downstream.Server.Describe;

/// <inheritdoc />
public sealed class ControlMapBuilder : IControlMapBuilder
{
    /// <inheritdoc />
    public void Rebuild(CameraStream camera)
    {
        camera.ControlToPayloadMap.Clear();
        if (camera.Sdp == null)
        {
            return;
        }

        int? currentPayload = null;
        foreach (string raw in camera.Sdp)
        {
            string line = raw.Trim();
            if (line.StartsWith("m="))
            {
                currentPayload = null;
            }
            else if (line.StartsWith("a=rtpmap:"))
            {
                string after = line["a=rtpmap:".Length..];
                string[] parts = after.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && int.TryParse(parts[0], out int pt))
                {
                    currentPayload = pt;
                }
            }
            else if (line.StartsWith("a=control:"))
            {
                string control = line["a=control:".Length..];
                if (currentPayload.HasValue && !camera.ControlToPayloadMap.ContainsKey(control))
                {
                    camera.ControlToPayloadMap[control] = currentPayload.Value;
                }

                if (control.StartsWith("trackID=", StringComparison.OrdinalIgnoreCase) && currentPayload.HasValue)
                {
                    camera.ControlToPayloadMap[control] = currentPayload.Value;
                }
            }
        }
    }
}