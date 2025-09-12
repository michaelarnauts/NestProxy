using System.Net.Security;

namespace NestProxy.Rtsp.Transport;

/// <summary>
/// Factory for creating RTSP transport instances based on URI scheme.
/// </summary>
public static class RtspTransportFactory
{
    private const int DefaultRtspPort = 554;
    private const int DefaultRtspsPort = 443;

    /// <summary>
    /// Creates an appropriate RTSP transport based on the URL scheme.
    /// </summary>
    /// <param name="url">The RTSP URL to connect to.</param>
    /// <param name="certificateValidationCallback">Optional callback for validating SSL certificates.</param>
    /// <returns>An <see cref="IRtspTransport"/> instance appropriate for the URL scheme.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the URL scheme is not supported.</exception>
    public static IRtspTransport CreateFromUrl(
        Uri url,
        RemoteCertificateValidationCallback? certificateValidationCallback = null)
    {
        ArgumentNullException.ThrowIfNull(url);

        string host = url.Host;
        int port = url.Port > 0 ? url.Port : GetDefaultPort(url.Scheme);

        return url.Scheme.ToLowerInvariant() switch
        {
            "rtsp" => new RtspTcpTransport(host, port),
            "rtsps" => new RtspSslTransport(host, port, certificateValidationCallback),
            _ => throw new ArgumentException($"Unsupported RTSP URL scheme: {url.Scheme}. Expected 'rtsp' or 'rtsps'.", nameof(url))
        };
    }

    /// <summary>
    /// Gets the default port for the specified RTSP scheme.
    /// </summary>
    /// <param name="scheme">The URL scheme (rtsp or rtsps).</param>
    /// <returns>The default port number.</returns>
    private static int GetDefaultPort(string scheme)
    {
        return scheme.ToLowerInvariant() switch
        {
            "rtsp" => DefaultRtspPort,
            "rtsps" => DefaultRtspsPort,
            _ => DefaultRtspPort
        };
    }
}
