using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;

using Shouldly;

using Xunit;

namespace NestProxy.Rtsp.Tests;

public class RtspServerTests
{
    [Fact]
    public void Constructor_InitializesProperties()
    {
        // Act
        var server = new RtspServer(8554);

        // Assert
        server.Port.ShouldBe(8554);
        server.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void RegisterHandler_AddsHandler()
    {
        // Arrange
        var server = new RtspServer(8554);
        var handlerCalled = false;
        Task<RtspResponse> handler(RtspRequest request, CancellationToken ct)
        {
            handlerCalled = true;
            return Task.FromResult(new RtspResponse(RtspStatusCode.OK, "OK", RtspVersion.Version1_0));
        }

        // Act
        server.RegisterHandler(RtspMethod.OPTIONS, handler);

        // Assert - just verify no exception
        handlerCalled.ShouldBeFalse(); // Not called yet
    }

    [Fact]
    public async Task StartAsync_StartsServer()
    {
        // Arrange
        var server = new RtspServer(0); // Use port 0 for dynamic allocation

        try
        {
            // Act
            await server.StartAsync();

            // Assert
            server.IsRunning.ShouldBeTrue();
        }
        finally
        {
            // Cleanup
            await server.StopAsync();
        }
    }

    [Fact]
    public async Task StopAsync_StopsServer()
    {
        // Arrange
        var server = new RtspServer(0);
        await server.StartAsync();

        // Act
        await server.StopAsync();

        // Assert
        server.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyRunning_ThrowsException()
    {
        // Arrange
        var server = new RtspServer(0);
        await server.StartAsync();

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await server.StartAsync());
        }
        finally
        {
            // Cleanup
            await server.StopAsync();
        }
    }
}
