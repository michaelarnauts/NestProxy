using System.Net;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Transport;

namespace NestProxy.Rtsp.Tests.Integration;

internal sealed class NoopRtspTransport : IRtspTransport
{
    public bool Connected => true;
    public string RemoteAddress => "127.0.0.1";
    public IPEndPoint RemoteEndPoint => new(IPAddress.Loopback, 8554);
    public IPEndPoint LocalEndPoint => new(IPAddress.Loopback, 8554);

    public uint NextCommandIndex() => 1;
    public void Reconnect() { }
    public void Close() { }
    public Stream GetStream() => Stream.Null;
    public Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken) => Task.FromResult(request.CreateResponse());
    public Task SendResponseAsync(RtspResponse response, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task<object> ReceiveMessageAsync(CancellationToken cancellationToken) => Task.FromResult<object>(new RtspResponse());
    public Task<byte> PeekByteAsync(CancellationToken cancellationToken) => Task.FromResult((byte)0);
    public Task<InterleavedData> ReceiveInterleavedDataAsync(CancellationToken cancellationToken) => Task.FromResult(new InterleavedData { Channel = 0, Data = [] });
    public Task SendInterleavedDataAsync(byte channel, ReadOnlyMemory<byte> data, CancellationToken cancellationToken) => Task.CompletedTask;
    public void Dispose() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
