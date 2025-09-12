using NestProxy.Rtsp.Messages;

namespace NestProxy.Rtsp.Protocol;

/// <summary>
/// Tracks outstanding RTSP requests to correlate responses with their original requests.
/// Uses CSeq header for correlation.
/// </summary>
public class RtspRequestTracker
{
    private readonly Dictionary<int, RtspRequest> _pendingRequests = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Tracks a request that was sent.
    /// </summary>
    /// <param name="request">The request to track.</param>
    public void TrackRequest(RtspRequest request)
    {
        if (request.CSeq == 0)
        {
            return; // Can't track requests without CSeq
        }

        lock (_lock)
        {
            _pendingRequests[request.CSeq] = request;
        }
    }

    /// <summary>
    /// Attempts to correlate a response with its original request using CSeq.
    /// </summary>
    /// <param name="response">The response to correlate.</param>
    /// <returns>True if the original request was found and set; otherwise false.</returns>
    public bool CorrelateResponse(RtspResponse response)
    {
        if (!response.Headers.TryGetValue("CSeq", out string? cseqStr) ||
            !int.TryParse(cseqStr, out int cseq))
        {
            return false;
        }

        lock (_lock)
        {
            if (_pendingRequests.TryGetValue(cseq, out RtspRequest? request))
            {
                response.OriginalRequest = request;
                _pendingRequests.Remove(cseq);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Clears all pending requests.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _pendingRequests.Clear();
        }
    }

    /// <summary>
    /// Gets the number of pending requests.
    /// </summary>
    public int PendingCount
    {
        get
        {
            lock (_lock)
            {
                return _pendingRequests.Count;
            }
        }
    }
}
