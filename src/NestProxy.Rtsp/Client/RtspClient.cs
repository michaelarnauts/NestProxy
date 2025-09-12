using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Transport;

namespace NestProxy.Rtsp.Client;

/// <summary>
/// High-level RTSP client for making RTSP requests.
/// Manages CSeq and provides convenient methods for common RTSP operations.
/// </summary>
public sealed class RtspClient : IDisposable, IAsyncDisposable
{
    private readonly IRtspTransport _transport;
    private int _cseq;
    private bool _disposed;

    public RtspClient(IRtspTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    /// <summary>
    /// Sends an OPTIONS request.
    /// </summary>
    public async Task<RtspResponse> OptionsAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.OPTIONS, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a DESCRIBE request.
    /// </summary>
    public async Task<RtspResponse> DescribeAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.DESCRIBE, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        request.Headers["Accept"] = "application/sdp";
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a SETUP request.
    /// </summary>
    /// <param name="uri">The track URI.</param>
    /// <param name="transport">The Transport header value.</param>
    /// <param name="sessionId">Optional session ID if already established.</param>
    public async Task<RtspResponse> SetupAsync(Uri uri, string transport, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.SETUP, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        request.Headers["Transport"] = transport;
        
        if (!string.IsNullOrEmpty(sessionId))
        {
            request.Headers["Session"] = sessionId;
        }
        
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a PLAY request.
    /// </summary>
    public async Task<RtspResponse> PlayAsync(Uri uri, string sessionId, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.PLAY, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        request.Headers["Session"] = sessionId;
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a PAUSE request.
    /// </summary>
    public async Task<RtspResponse> PauseAsync(Uri uri, string sessionId, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.PAUSE, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        request.Headers["Session"] = sessionId;
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a TEARDOWN request.
    /// </summary>
    public async Task<RtspResponse> TeardownAsync(Uri uri, string sessionId, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.TEARDOWN, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        request.Headers["Session"] = sessionId;
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a GET_PARAMETER request (commonly used for keep-alive).
    /// </summary>
    public async Task<RtspResponse> GetParameterAsync(Uri uri, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var request = new RtspRequest(RtspMethod.GET_PARAMETER, uri, RtspVersion.Version1_0);
        request.CSeq = Interlocked.Increment(ref _cseq);
        
        if (!string.IsNullOrEmpty(sessionId))
        {
            request.Headers["Session"] = sessionId;
        }
        
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    /// <summary>
    /// Sends a custom RTSP request.
    /// </summary>
    public async Task<RtspResponse> SendRequestAsync(RtspRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        
        if (request.CSeq == 0)
        {
            request.CSeq = Interlocked.Increment(ref _cseq);
        }
        
        return await _transport.SendRequestAsync(request, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _transport.Dispose();
        _disposed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _transport.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
