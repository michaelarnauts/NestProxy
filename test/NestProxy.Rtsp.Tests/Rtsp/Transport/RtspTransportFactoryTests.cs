using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

using NestProxy.Rtsp.Transport;

using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Transport;

public sealed class RtspTransportFactoryTests
{
    [Fact]
    public void CreateFromUrl_WithNullUrl_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => RtspTransportFactory.CreateFromUrl(null!));
    }

    [Theory]
    [InlineData("rtsp://localhost:554/stream")]
    [InlineData("rtsp://192.168.1.100/camera")]
    [InlineData("rtsp://example.com:8554/live")]
    public void CreateFromUrl_WithRtspScheme_ReturnsRtspTcpTransport(string urlString)
    {
        Uri url = new(urlString);

        using IRtspTransport transport = RtspTransportFactory.CreateFromUrl(url);

        Assert.NotNull(transport);
        Assert.IsType<RtspTcpTransport>(transport);
    }

    [Theory]
    [InlineData("rtsps://localhost:322/stream")]
    [InlineData("rtsps://192.168.1.100/camera")]
    [InlineData("rtsps://example.com:8322/live")]
    public void CreateFromUrl_WithRtspsScheme_ReturnsRtspSslTransport(string urlString)
    {
        Uri url = new(urlString);

        using IRtspTransport transport = RtspTransportFactory.CreateFromUrl(url);

        Assert.NotNull(transport);
        Assert.IsType<RtspSslTransport>(transport);
    }

    [Theory]
    [InlineData("http://localhost/stream")]
    [InlineData("https://example.com/camera")]
    [InlineData("ftp://server/file")]
    public void CreateFromUrl_WithUnsupportedScheme_ThrowsArgumentException(string urlString)
    {
        Uri url = new(urlString);

        ArgumentException ex = Assert.Throws<ArgumentException>(() => RtspTransportFactory.CreateFromUrl(url));
        Assert.Contains("scheme", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateFromUrl_WithRtspDefaultPort_UsesPort554()
    {
        Uri url = new("rtsp://localhost/stream");

        using RtspTcpTransport transport = (RtspTcpTransport)RtspTransportFactory.CreateFromUrl(url);

        Assert.NotNull(transport);
    }

    [Fact]
    public void CreateFromUrl_WithRtspsDefaultPort_UsesPort322()
    {
        Uri url = new("rtsps://localhost/stream");

        using RtspSslTransport transport = (RtspSslTransport)RtspTransportFactory.CreateFromUrl(url);

        Assert.NotNull(transport);
    }

    [Fact]
    public void CreateFromUrl_WithExplicitPort_UsesSpecifiedPort()
    {
        Uri url = new("rtsp://localhost:8554/stream");

        using RtspTcpTransport transport = (RtspTcpTransport)RtspTransportFactory.CreateFromUrl(url);

        Assert.NotNull(transport);
    }

    [Fact]
    public void CreateFromUrl_WithCertificateCallback_PassesToSslTransport()
    {
        Uri url = new("rtsps://localhost/stream");
        static bool CertCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;

        using IRtspTransport transport = RtspTransportFactory.CreateFromUrl(url, CertCallback);

        Assert.NotNull(transport);
        Assert.IsType<RtspSslTransport>(transport);
    }

    [Fact]
    public void CreateFromUrl_WithCertificateCallbackForTcp_IgnoresCallback()
    {
        Uri url = new("rtsp://localhost/stream");
        static bool CertCallback(object sender, X509Certificate? cert, X509Chain? chain, SslPolicyErrors errors) => true;

        using IRtspTransport transport = RtspTransportFactory.CreateFromUrl(url, CertCallback);

        Assert.NotNull(transport);
        Assert.IsType<RtspTcpTransport>(transport);
    }

    [Theory]
    [InlineData("RTSP://LOCALHOST/STREAM")]
    [InlineData("RtSp://localhost/stream")]
    [InlineData("RTSPS://LOCALHOST/STREAM")]
    [InlineData("RtSpS://localhost/stream")]
    public void CreateFromUrl_WithMixedCaseScheme_HandlesCorrectly(string urlString)
    {
        Uri url = new(urlString);

        using IRtspTransport transport = RtspTransportFactory.CreateFromUrl(url);

        Assert.NotNull(transport);
    }
}
