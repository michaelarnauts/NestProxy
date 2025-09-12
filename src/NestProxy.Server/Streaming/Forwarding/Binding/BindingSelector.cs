using System.Collections.Concurrent;

namespace NestProxy.Server.Streaming.Forwarding.Binding;

/// <summary>
/// Maintains mappings between RTP SSRC values and downstream transport bindings (SSRC-first) so that RTP and
/// RTCP packets for a given source are routed consistently to the same <see cref="TrackBinding"/>. Payload type
/// is treated purely as a bootstrap hint: if only a single binding exists for a payload type when an unknown
/// SSRC is first observed, that binding is associated. If multiple bindings share the payload type, no
/// association is made until some other mechanism (e.g. first RTCP report correlation or later deterministic
/// selection we might add) identifies the correct binding. This removes previous round-robin behaviour so that
/// binding decisions are stable, reproducible, and never arbitrarily re-ordered across restarts.
/// Thread-safe via concurrent dictionaries; no per-packet locking.
/// </summary>
public sealed class BindingSelector
{
    private readonly ConcurrentDictionary<(string path, string session, uint ssrc), int> _ssrcToPayloadType = new();
    private readonly ConcurrentDictionary<(string path, string session, uint ssrc), TrackBinding> _ssrcToBinding = new();

    /// <summary>Remembers the RTP payload type associated with an SSRC inside a session for later RTCP routing.</summary>
    /// <param name="cameraPath">Logical camera path.</param>
    /// <param name="sessionId">Downstream session identifier.</param>
    /// <param name="ssrc">RTP SSRC value.</param>
    /// <param name="payloadType">RTP payload type number.</param>
    public void RememberPayloadType(string cameraPath, string sessionId, uint ssrc, int payloadType)
    {
        _ssrcToPayloadType[(cameraPath, sessionId, ssrc)] = payloadType;
    }

    /// <summary>Attempts to retrieve a previously remembered payload type for an SSRC.</summary>
    /// <param name="cameraPath">Logical camera path.</param>
    /// <param name="sessionId">Downstream session identifier.</param>
    /// <param name="ssrc">RTP SSRC value.</param>
    /// <returns>The payload type if known; otherwise <c>null</c>.</returns>
    public int? TryGetPayloadType(string cameraPath, string sessionId, uint ssrc)
    {
        return _ssrcToPayloadType.TryGetValue((cameraPath, sessionId, ssrc), out int payloadType) ? payloadType : null;
    }

    /// <summary>
    /// SSRC-first selection: returns an existing binding for the SSRC or (if unknown) attempts a bootstrap
    /// association using the supplied binding list. If exactly one binding exists in the list it is selected
    /// and pinned to this SSRC. If zero or multiple bindings exist, no association is made and <c>null</c>
    /// is returned so the caller can fall back to a session-level transport.
    /// </summary>
    /// <param name="cameraPath">Logical camera path.</param>
    /// <param name="sessionId">Downstream session identifier.</param>
    /// <param name="ssrc">RTP SSRC value.</param>
    /// <param name="bindings">Candidate bindings for the payload type.</param>
    /// <returns>The chosen binding; or <c>null</c> if none available.</returns>
    public TrackBinding? GetOrChooseBinding(string cameraPath, string sessionId, uint ssrc, List<TrackBinding> bindings)
    {
        if (bindings == null || bindings.Count == 0)
        {
            return null;
        }

        if (_ssrcToBinding.TryGetValue((cameraPath, sessionId, ssrc), out TrackBinding? existing))
        {
            return existing;
        }

        if (bindings.Count == 1)
        {
            TrackBinding only = bindings[0];
            _ssrcToBinding[(cameraPath, sessionId, ssrc)] = only;
            return only;
        }

        return null;
    }

    /// <summary>Tries to retrieve a previously selected binding for the given SSRC.</summary>
    /// <param name="cameraPath">Logical camera path.</param>
    /// <param name="sessionId">Downstream session identifier.</param>
    /// <param name="ssrc">RTP SSRC value.</param>
    /// <returns>The associated binding if known; otherwise <c>null</c>.</returns>
    public TrackBinding? TryGetBindingBySsrc(string cameraPath, string sessionId, uint ssrc)
    {
        return _ssrcToBinding.TryGetValue((cameraPath, sessionId, ssrc), out TrackBinding? binding) ? binding : null;
    }

    /// <summary>
    /// Associates an SSRC directly with a binding (used if an alternative resolution path discovered mapping
    /// without needing round-robin on payload type).
    /// </summary>
    public void AssociateSsrc(string cameraPath, string sessionId, uint ssrc, TrackBinding binding, int payloadType)
    {
        _ssrcToBinding[(cameraPath, sessionId, ssrc)] = binding;
        _ssrcToPayloadType[(cameraPath, sessionId, ssrc)] = payloadType;
    }

    /// <summary>
    /// Clears all SSRC→binding and SSRC→payload-type associations for a given camera path across all sessions.
    /// This is required when the upstream source switches (e.g., staging session promoted to primary during
    /// token refresh) so that downstream clients can re-learn the new SSRCs without stale mappings interfering.
    /// </summary>
    /// <param name="cameraPath">Logical camera path whose associations should be cleared.</param>
    public void ClearCamera(string cameraPath)
    {
        List<(string path, string session, uint ssrc)> keysToRemove = [];

        foreach (var key in _ssrcToBinding.Keys)
        {
            if (key.path == cameraPath)
            {
                keysToRemove.Add(key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _ssrcToBinding.TryRemove(key, out _);
            _ssrcToPayloadType.TryRemove(key, out _);
        }
    }
}