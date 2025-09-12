using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NestProxy.Rtsp.Transport;

/// <summary>
/// SSL/TLS wrapper for RTSP TCP transport.
/// Extends <see cref="RtspTcpTransport"/> with secure communication capabilities.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="RtspSslTransport"/> class.
/// </remarks>
/// <param name="host">The hostname or IP address to connect to.</param>
/// <param name="port">The port number to connect to.</param>
/// <param name="certificateValidationCallback">Optional callback for validating the remote certificate.</param>
public sealed class RtspSslTransport(string host, int port, RemoteCertificateValidationCallback? certificateValidationCallback = null) 
    : RtspTcpTransport(host, port)
{
    private SslStream? _sslStream;

    /// <summary>
    /// Establishes the TCP connection and performs SSL/TLS handshake.
    /// </summary>
    protected override void Connect()
    {
        // First establish TCP connection.
        base.Connect();

        // Wrap the TCP stream with SSL.
        Stream baseStream = base.GetStream();

        _sslStream = new SslStream(
            baseStream,
            leaveInnerStreamOpen: false,
            certificateValidationCallback ?? DefaultCertificateValidation);

        // Perform SSL/TLS handshake.
        // Use the original hostname for SNI (Server Name Indication), not the resolved IP.
        _sslStream.AuthenticateAsClient(Host);
    }

    /// <inheritdoc />
    public override Stream GetStream()
    {
        if (_sslStream is null)
        {
            Connect();
        }

        return _sslStream!;
    }

    /// <inheritdoc />
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        Stream stream = GetStream();
        return stream.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc />
    public override Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        Stream stream = GetStream();
        return stream.WriteAsync(buffer, cancellationToken).AsTask();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sslStream?.Dispose();
            _sslStream = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeAsyncCore()
    {
        if (_sslStream is not null)
        {
            await _sslStream.DisposeAsync().ConfigureAwait(false);
            _sslStream = null;
        }

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    /// <summary>
    /// Default certificate validation callback that accepts all certificates.
    /// </summary>
    /// <remarks>
    /// In production, proper certificate validation should be implemented.
    /// </remarks>
    private static bool DefaultCertificateValidation(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        // For now, accept all certificates (matches existing behavior).
        // TODO: Implement proper certificate validation for production use.
        return true;
    }
}
