using System.Threading.Tasks;
using NestProxy.Rtsp.Client;
using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Protocol;

namespace NestProxy.Server.Streaming.Upstream;

/// <summary>
/// Manages the RTSP protocol state machine for an upstream session, handling the
/// OPTIONS > DESCRIBE > SETUP(s) > PLAY sequence with interleaved channel assignment.
/// </summary>
/// <param name="cameraId">Camera identifier for logging (local endpoint path).</param>
/// <param name="instanceId">Session instance identifier for logging.</param>
/// <param name="logger">Logger instance.</param>
public sealed class UpstreamSessionStateMachine(string cameraId, string instanceId, ILogger logger) : IUpstreamSessionStateMachine
{
    private readonly Queue<Uri> _setupQueue = new();
    private readonly List<(int data, int control)> _interleaved = [];
    private bool _awaitingSetup;
    private bool _usingSessionLevel;
    private int _nextInterleaved;
    private List<Uri> _trackUris = [];

    /// <summary>
    /// RTSP Session header value once established (null until first successful SETUP).
    /// </summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Content-Base (or Location) derived base URL resolved from DESCRIBE.
    /// </summary>
    public string? BaseUrl { get; private set; }

    /// <summary>
    /// Aggregate (session-level) URL if provided (used for PLAY / session-level SETUP fallback).
    /// </summary>
    public string? AggregateUrl { get; private set; }

    /// <summary>
    /// Ordered list of (RTP, RTCP) interleaved channel pairs assigned per track.
    /// </summary>
    public IReadOnlyList<(int data, int control)> InterleavedAssignments => _interleaved;

    /// <summary>
    /// Raised after DESCRIBE is parsed and control URLs / track URIs are resolved.
    /// </summary>
    public event Action<SdpResolutionResult>? DescribeResolved;

    /// <summary>
    /// Resets the state machine to initial conditions.
    /// </summary>
    public void Reset()
    {
        SessionId = null;
        BaseUrl = null;
        AggregateUrl = null;

        _interleaved.Clear();
        _setupQueue.Clear();
        _trackUris.Clear();

        _awaitingSetup = false;
        _usingSessionLevel = false;

        _nextInterleaved = 0;
    }

    /// <inheritdoc />
    public async Task HandleResponseAsync(RtspConnection connection, RtspResponse response, IRtspResponseFormatter formatter)
    {
        RtspRequest? orig = response.OriginalRequest;
        if (orig == null)
        {
            logger.LogWarning("[{CameraId}] [sess-{Sess}] Response has no OriginalRequest! CSeq: {CSeq}, ReturnCode: {Code}.",
                cameraId,
                instanceId,
                response.Headers.TryGetValue("CSeq", out string? cseq) ? cseq : "none",
                response.ReturnCode);
            return;
        }

        logger.LogDebug("[{CameraId}] [sess-{Sess}] Processing {Method} response", cameraId, instanceId, orig.Method);

        switch (orig.Method)
        {
            case "OPTIONS":
                await HandleOptionsResponseAsync(connection, response).ConfigureAwait(false);
                break;
            case "DESCRIBE":
                string describeHeaders = formatter.FormatResponseHeaders(response, "CSeq", "Content-Base", "Content-Location", "Content-Length", "Session");
                string body = formatter.TruncateBody(response.Data, 4096);
                logger.LogInformation("[{CameraId}] [sess-{Sess}] Server responded {Code} {Phrase} to DESCRIBE request. Headers: {Headers} body:\n{Body}", cameraId, instanceId, response.ReturnCode, response.ReasonPhrase, describeHeaders, body);
                await HandleDescribeResponseAsync(connection, response).ConfigureAwait(false);
                break;
            case "SETUP":
                string setupHeaders = formatter.FormatResponseHeaders(response, "CSeq", "Session", "Transport", "Content-Length");
                logger.LogInformation("[{CameraId}] [sess-{Sess}] Server responded {Code} {Phrase} to SETUP request. Headers: {Headers}.", cameraId, instanceId, response.ReturnCode, response.ReasonPhrase, setupHeaders);
                await HandleSetupResponseAsync(connection, response).ConfigureAwait(false);
                break;
            case "PLAY":
                string playHeaders = formatter.FormatResponseHeaders(response, "CSeq", "Session", "Range");
                logger.LogInformation("[{CameraId}] [sess-{Sess}] Server responded {Code} {Phrase} to PLAY request. Headers: {Headers}.", cameraId, instanceId, response.ReturnCode, response.ReasonPhrase, playHeaders);
                break;
            case "TEARDOWN":
                string teardownHeaders = formatter.FormatResponseHeaders(response, "CSeq", "Session");
                logger.LogInformation("[{CameraId}] [sess-{Sess}] Server responded {Code} {Phrase} to TEARDOWN request. Headers: {Headers}.", cameraId, instanceId, response.ReturnCode, response.ReasonPhrase, teardownHeaders);
                break;
        }
    }

    private async Task HandleOptionsResponseAsync(RtspConnection connection, RtspResponse response)
    {
        RtspRequest? orig = response.OriginalRequest;
        if (BaseUrl == null && orig?.RtspUri != null)
        {
            BaseUrl = orig.RtspUri.ToString();
            RtspRequest describe = RtspRequest.CreateDescribe(orig.RtspUri);
            logger.LogInformation("[{CameraId}] [sess-{Sess}] Sending DESCRIBE {Url}.", cameraId, instanceId, describe.RtspUri);
            await connection.SendMessageAsync(describe).ConfigureAwait(false);
        }
    }

    private async Task HandleDescribeResponseAsync(RtspConnection connection, RtspResponse response)
    {
        try
        {
            RtspRequest? orig = response.OriginalRequest;
            SdpResolutionResult res = SdpControlResolver.Resolve(response, orig, BaseUrl);
            BaseUrl = res.BaseUrl;
            AggregateUrl = res.AggregateUrl;
            _trackUris = [.. res.TrackUris];

            DescribeResolved?.Invoke(res);

            _setupQueue.Clear();
            foreach (Uri u in _trackUris)
            {
                _setupQueue.Enqueue(u);
            }

            _nextInterleaved = 0;
            _interleaved.Clear();
            _awaitingSetup = false;
            _usingSessionLevel = false;
            await StartNextSetupAsync(connection).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[{CameraId}] [sess-{Sess}] Failed to process DESCRIBE response.", cameraId, instanceId);
        }
    }

    private async Task HandleSetupResponseAsync(RtspConnection connection, RtspResponse response)
    {
        if (response.ReturnCode >= 200 && response.ReturnCode < 300)
        {
            if (response.Headers.TryGetValue("Session", out string? session) && !string.IsNullOrEmpty(session))
            {
                SessionId = session;
            }

            _awaitingSetup = false;
            if (_setupQueue.Count > 0)
            {
                await StartNextSetupAsync(connection).ConfigureAwait(false);
            }
            else
            {
                await SendPlayOnceAsync(connection).ConfigureAwait(false);
            }
        }
        else
        {
            // Fallback to session-level SETUP on track-level failure
            if (!_usingSessionLevel)
            {
                _usingSessionLevel = true;
                _setupQueue.Clear();
                if (BaseUrl != null)
                {
                    _setupQueue.Enqueue(new Uri(BaseUrl));
                }

                _awaitingSetup = false;
                await StartNextSetupAsync(connection).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Returns the first control (RTCP) channel number if assigned.
    /// </summary>
    /// <param name="controlChannel">The control channel number.</param>
    /// <returns>True if a control channel is assigned; otherwise false.</returns>
    public bool TryGetFirstControlChannel(out int controlChannel)
    {
        if (_interleaved.Count > 0)
        {
            controlChannel = _interleaved[0].control;
            return true;
        }

        controlChannel = default;
        return false;
    }

    private async Task StartNextSetupAsync(RtspConnection connection)
    {
        if (_awaitingSetup || _setupQueue.Count == 0)
        {
            if (_setupQueue.Count == 0)
            {
                await SendPlayOnceAsync(connection).ConfigureAwait(false);
            }

            return;
        }

        Uri uri = _setupQueue.Dequeue();
        int dataCh = _nextInterleaved;
        int ctrlCh = _nextInterleaved + 1;
        string transport = $"RTP/AVP/TCP;interleaved={dataCh}-{ctrlCh}";

        RtspRequest setup = RtspRequest.CreateSetup(uri, transport);
        if (!string.IsNullOrEmpty(SessionId))
        {
            setup.Headers["Session"] = SessionId;
        }

        logger.LogInformation("[{CameraId}] [sess-{Sess}] Sending SETUP {Uri} interleaved={First}-{Second}.", cameraId, instanceId, uri, dataCh, ctrlCh);
        _interleaved.Add((dataCh, ctrlCh));
        _nextInterleaved += 2;
        _awaitingSetup = true;

        try
        {
            await connection.SendMessageAsync(setup).ConfigureAwait(false);
        }
        catch
        {
            _awaitingSetup = false;
            throw;
        }
    }

    private Task SendPlayOnceAsync(RtspConnection connection)
    {
        if (string.IsNullOrEmpty(SessionId) || string.IsNullOrEmpty(BaseUrl))
        {
            return Task.CompletedTask;
        }

        string target = AggregateUrl ?? BaseUrl!;
        RtspRequest play = RtspRequest.CreatePlay(new Uri(target), SessionId);
        logger.LogInformation("[{CameraId}] [sess-{Sess}] Sending PLAY {Url}.", cameraId, instanceId, target);
        return connection.SendMessageAsync(play);
    }
}
