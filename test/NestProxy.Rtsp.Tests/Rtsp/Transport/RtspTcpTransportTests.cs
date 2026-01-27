using System.Net;
using System.Net.Sockets;

using NestProxy.Rtsp.Transport;

using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Transport;

public sealed class RtspTcpTransportTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        using RtspTcpTransport transport = new("localhost", 8554);

        Assert.NotNull(transport);
        Assert.False(transport.Connected);
    }

    [Fact]
    public void Constructor_WithNullHost_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentNullException>(() => new RtspTcpTransport(null!, 8554));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WithEmptyHost_ThrowsArgumentException()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(() => new RtspTcpTransport(string.Empty, 8554));
        Assert.Contains("host", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void Constructor_WithInvalidPort_ThrowsArgumentOutOfRangeException(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RtspTcpTransport("localhost", port));
    }

    [Fact]
    public void RemoteAddress_BeforeConnection_ReturnsNone()
    {
        using RtspTcpTransport transport = new("localhost", 8554);

        string remoteAddress = transport.RemoteAddress;

        Assert.Equal(IPAddress.None.ToString(), remoteAddress);
    }

    [Fact]
    public void RemoteEndPoint_BeforeConnection_ReturnsNoneEndpoint()
    {
        using RtspTcpTransport transport = new("localhost", 8554);

        IPEndPoint endpoint = transport.RemoteEndPoint;

        Assert.Equal(IPAddress.None, endpoint.Address);
        Assert.Equal(0, endpoint.Port);
    }

    [Fact]
    public void LocalEndPoint_BeforeConnection_ReturnsNoneEndpoint()
    {
        using RtspTcpTransport transport = new("localhost", 8554);

        IPEndPoint endpoint = transport.LocalEndPoint;

        Assert.Equal(IPAddress.None, endpoint.Address);
        Assert.Equal(0, endpoint.Port);
    }

    [Fact]
    public void NextCommandIndex_IncrementsSequentially()
    {
        using RtspTcpTransport transport = new("localhost", 8554);

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
        using RtspTcpTransport transport = new("localhost", 8554);

        transport.Close();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        RtspTcpTransport transport = new("localhost", 8554);

        transport.Dispose();
        transport.Dispose();
    }

    [Fact]
    public async Task GetStream_WithNoServer_ThrowsSocketException()
    {
        using RtspTcpTransport transport = new("localhost", 12345);

        SocketException ex = Assert.Throws<SocketException>(() => transport.GetStream());
        Assert.NotNull(ex);
    }

    [Fact]
    public async Task WriteAsync_WithBuffer_ThrowsWhenNotConnected()
    {
        using RtspTcpTransport transport = new("localhost", 12345);
        byte[] buffer = [0x01, 0x02, 0x03];

        await Assert.ThrowsAsync<SocketException>(
            async () => await transport.WriteAsync(buffer, 0, buffer.Length, CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_WithMemory_ThrowsWhenNotConnected()
    {
        using RtspTcpTransport transport = new("localhost", 12345);
        byte[] buffer = [0x01, 0x02, 0x03];

        await Assert.ThrowsAsync<SocketException>(
            async () => await transport.WriteAsync(buffer.AsMemory(), CancellationToken.None));
    }

    [Fact]
    public async Task WriteAsync_WithNullBuffer_ThrowsArgumentNullException()
    {
        using RtspTcpTransport transport = new("localhost", 8554);

        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await transport.WriteAsync(null!, 0, 0, CancellationToken.None));
    }

    [Fact]
    public async Task ConnectAndWrite_WithLocalServer_Succeeds()
    {
        using TcpListener server = new(IPAddress.Loopback, 0);
        server.Start();
        int port = ((IPEndPoint)server.LocalEndpoint).Port;

        ValueTask<TcpClient> acceptTask = server.AcceptTcpClientAsync(TestContext.Current.CancellationToken);

        using RtspTcpTransport transport = new("127.0.0.1", port);

        Stream stream = transport.GetStream();

        using TcpClient serverClient = await acceptTask;
        Assert.True(transport.Connected);

        byte[] data = "TEST"u8.ToArray();
        await transport.WriteAsync(data, 0, data.Length, CancellationToken.None);

        byte[] received = new byte[4];
        NetworkStream serverStream = serverClient.GetStream();
        int bytesRead = await serverStream.ReadAsync(received, 0, 4, CancellationToken.None);

        Assert.Equal(4, bytesRead);
        Assert.Equal(data, received);
        Assert.True(transport.Connected);
    }

    [Fact]
    public async Task Reconnect_AfterClose_Succeeds()
    {
        using TcpListener server = new(IPAddress.Loopback, 0);
        server.Start();
        int port = ((IPEndPoint)server.LocalEndpoint).Port;

        using RtspTcpTransport transport = new("127.0.0.1", port);

        ValueTask<TcpClient> acceptTask1 = server.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        Stream stream1 = transport.GetStream();
        TcpClient serverClient1 = await acceptTask1;
        Assert.True(transport.Connected);

        transport.Close();
        Assert.False(transport.Connected);

        ValueTask<TcpClient> acceptTask2 = server.AcceptTcpClientAsync(TestContext.Current.CancellationToken);
        transport.Reconnect();
        TcpClient serverClient2 = await acceptTask2;

        Assert.True(transport.Connected);
        serverClient1.Dispose();
        serverClient2.Dispose();
    }
}
