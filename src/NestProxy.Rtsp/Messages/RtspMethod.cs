namespace NestProxy.Rtsp.Messages;

/// <summary>
/// RTSP method constants.
/// </summary>
public static class RtspMethod
{
    public const string OPTIONS = "OPTIONS";
    public const string DESCRIBE = "DESCRIBE";
    public const string ANNOUNCE = "ANNOUNCE";
    public const string SETUP = "SETUP";
    public const string PLAY = "PLAY";
    public const string PAUSE = "PAUSE";
    public const string TEARDOWN = "TEARDOWN";
    public const string GET_PARAMETER = "GET_PARAMETER";
    public const string SET_PARAMETER = "SET_PARAMETER";
    public const string REDIRECT = "REDIRECT";
    public const string RECORD = "RECORD";

    /// <summary>
    /// Parses a method string (simply returns it, with validation).
    /// </summary>
    public static string Parse(string method)
    {
        return !string.IsNullOrWhiteSpace(method)
            ? method.ToUpperInvariant()
            : throw new ArgumentException("Method cannot be null or empty", nameof(method));
    }
}
