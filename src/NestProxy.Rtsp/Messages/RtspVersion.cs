namespace NestProxy.Rtsp.Messages;

/// <summary>
/// RTSP version constants.
/// </summary>
public static class RtspVersion
{
    public const string Version1_0 = "RTSP/1.0";
    public const string Version2_0 = "RTSP/2.0";

    /// <summary>
    /// Parses a version string (validates and returns it).
    /// </summary>
    public static string Parse(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            throw new ArgumentException("Version cannot be null or empty", nameof(version));
        }
        
        if (!version.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Invalid RTSP version: {version}", nameof(version));
        }
        
        return version;
    }
}
