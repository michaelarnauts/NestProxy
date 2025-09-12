using System.Text;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using NestProxy.Rtsp.Protocol;

namespace NestProxy.Benchmarks;

[MemoryDiagnoser]
public class ParserBenchmarks
{
    private readonly RtspMessageParser _parser = new();
    private MemoryStream _optionsStream = null!;
    private MemoryStream _responseStream = null!;
    private MemoryStream _interleavedStream = null!;

    [GlobalSetup]
    public void Setup()
    {
        string options = "OPTIONS rtsp://example.com:554/stream RTSP/1.0\r\n" +
                         "CSeq: 1\r\n" +
                         "\r\n";
        _optionsStream = new MemoryStream(Encoding.UTF8.GetBytes(options));

        string body = "v=0\r\no=- 1 1 IN IP4 127.0.0.1\r\n";
        string response = "RTSP/1.0 200 OK\r\n" +
                          "CSeq: 2\r\n" +
                          $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                          "Content-Type: application/sdp\r\n" +
                          "\r\n" +
                          body;
        _responseStream = new MemoryStream(Encoding.UTF8.GetBytes(response));

        byte[] frameData = [0x80, 0x60, 0x12, 0x34, 0x56, 0x78];
        byte[] interleaved = new byte[10];
        interleaved[0] = (byte)'$';
        interleaved[1] = 2;
        interleaved[2] = 0;
        interleaved[3] = 6;
        Array.Copy(frameData, 0, interleaved, 4, frameData.Length);
        _interleavedStream = new MemoryStream(interleaved);
    }

    [Benchmark]
    public async Task ParseOptions()
    {
        _optionsStream.Position = 0;
        await _parser.ParseAsync(_optionsStream);
        _parser.Reset();
    }

    [Benchmark]
    public async Task ParseDescribeResponse()
    {
        _responseStream.Position = 0;
        await _parser.ParseAsync(_responseStream);
        _parser.Reset();
    }

    [Benchmark]
    public async Task ParseInterleavedFrame()
    {
        _interleavedStream.Position = 0;
        await _parser.ParseAsync(_interleavedStream);
        _parser.Reset();
    }
}
