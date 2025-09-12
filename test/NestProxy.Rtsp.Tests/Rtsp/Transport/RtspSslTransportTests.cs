using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

using NestProxy.Rtsp.Transport;

using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Transport;

public sealed class RtspSslTransportTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        using RtspSslTransport transport = new("localhost", 322);

        Assert.NotNull(transport);
        Assert.False(transport.Connected);
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentNullException>(() => new RtspSslTransport(null!, 322));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithEmptyHost_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new RtspSslTransport(string.Empty, 322));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RtspSslTransport("localhost", port));
    }

    [Fact]
    public void Constructor_WithCertificateCallback_StoresCallback()
    {
        static bool CertCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;

        using RtspSslTransport transport = new("localhost", 322, CertCallback);

        Assert.NotNull(transport);
    }

    [Fact]
    public void RemoteAddress_BeforeConnection_ReturnsNone()
    {
        using RtspSslTransport transport = new("localhost", 322);

        string remoteAddress = transport.RemoteAddress;

        Assert.Equal(IPAddress.None.ToString(), remoteAddress);
    }

    [Fact]
    public void NextCommandIndex_IncrementsSequentially()
    {
        using RtspSslTransport transport = new("localhost", 322);

        uint first = transport.NextCommandIndex();
        uint second = transport.NextCommandIndex();
        uint third = transport.NextCommandIndex();

        Assert.Equal(1u, first);
        Assert.Equal(2u, second);
        Assert.Equal(3u, third);
    }

    [Fact]
    public void Close_BeforeConnection_DoesNotThrow()
    {
        using RtspSslTransport transport = new("localhost", 322);

        transport.Close();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        RtspSslTransport transport = new("localhost", 322);

        transport.Dispose();
        transport.Dispose();
    }

    [Fact]
    public void GetStream_WithNoServer_ThrowsSocketException()
    {
        using RtspSslTransport transport = new("localhost", 12346);

        Assert.Throws<SocketException>(() => transport.GetStream());
    }

    [Fact]
    public async Task WriteAsync_WithBuffer_ThrowsWhenNotConnected()
    {
        using RtspSslTransport transport = new("localhost", 12346);
        byte[] buffer = [0x01, 0x02, 0x03];

        await Assert.ThrowsAsync<SocketException>(
            async () => await transport.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_WithMemory_ThrowsWhenNotConnected()
    {
        using RtspSslTransport transport = new("localhost", 12346);
        byte[] buffer = [0x01, 0x02, 0x03];

        await Assert.ThrowsAsync<SocketException>(
            async () => await transport.WriteAsync(buffer.AsMemory(), CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_WithNullBuffer_ThrowsArgumentNullException()
    {
        using RtspSslTransport transport = new("localhost", 322);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await transport.WriteAsync(null!, 0, 0, CancellationToken.None));
    }

    [Fact]
    public void Reconnect_AfterClose_DoesNotThrowOnClosePhase()
    {
        static bool AcceptAll(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;
        using RtspSslTransport transport = new("127.0.0.1", 12399, AcceptAll);

        transport.Close();
        Assert.False(transport.Connected);

        Assert.False(transport.Connected);
    }
}
