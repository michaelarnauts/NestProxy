using System.Text;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Sdp;

namespace NestProxy.Server.Streaming.Upstream.Sdp;

/// <summary>
/// Provides functionality to parse SDP data from RTSP responses and resolve aggregate and track URIs, as well as
/// relevant payload types, for media session setup.
/// </summary>
/// <remarks>
/// The SdpControlResolver class assists in interpreting SDP (Session Description Protocol) information received
/// in RTSP responses. It determines the appropriate base URL for resolving track and aggregate URIs by considering
/// the 'Content-Base' or 'Content-Location' headers, the original request URI, or a provided base URL. The resolved
/// URIs are suitable for use in subsequent RTSP SETUP requests. This class is intended for use in RTSP client
/// implementations that require detailed SDP parsing and URI resolution for media session negotiation.
/// </remarks>
public static class SdpControlResolver
{
    /// <summary>
    /// Parses the SDP data from an RTSP response and resolves the aggregate and track URIs, as well as relevant payload
    /// types, for media session setup.
    /// </summary>
    /// <remarks>
    /// The method uses the 'Content-Base' or 'Content-Location' headers from the response, if present, to determine the
    /// base URL for resolving track and aggregate URIs. If neither header is available, it falls back to the original
    /// request URI or the provided current base. The returned track URIs are suitable for use in subsequent RTSP SETUP
    /// requests.
    /// </remarks>
    /// <param name="response">The RTSP response containing the SDP data to be parsed. Must not be null.</param>
    /// <param name="originalRequest">The original RTSP request that initiated the response, or null if unavailable. Used to determine the base URL
    /// for resolving relative URIs.</param>
    /// <param name="currentBase">The current base URL to use for resolving relative URIs if the original request is not provided. May be null.</param>
    /// <returns>An SdpResolutionResult containing the resolved base URL, aggregate URL, track URIs, SDP lines, and payload types
    /// for video and audio streams.</returns>
    public static SdpResolutionResult Resolve(RtspResponse response, RtspRequest? originalRequest, string? currentBase)
    {
        string requestUrl = originalRequest?.RtspUri?.ToString() ?? currentBase ?? string.Empty;
        string? contentBase = null;
        string? contentLocation = null;
        if (response.Headers != null)
        {
            if (response.Headers.TryGetValue("Content-Base", out var cb) && !string.IsNullOrWhiteSpace(cb))
            {
                contentBase = cb.Trim();
            }

            if (response.Headers.TryGetValue("Content-Location", out var cl) && !string.IsNullOrWhiteSpace(cl))
            {
                contentLocation = cl.Trim();
            }
        }

        string baseForResolution = !string.IsNullOrEmpty(contentBase) ? contentBase! : (!string.IsNullOrEmpty(contentLocation) ? contentLocation! : requestUrl);

        byte[] bytes = response.Data.Length == 0 ? [] : response.Data.ToArray();
        string sdpContent = Encoding.UTF8.GetString(bytes);

        var parser = new SdpParser();
        SdpFile sdp = parser.Parse(sdpContent);

        string[] sdpLines = sdpContent.Replace("\r", string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? sessionControl = sdp.GetAttributeValue("control");

        string? aggregateUrl = ResolveAggregateUrl(baseForResolution, sessionControl);

        List<Uri> tracks = [];
        int? videoPt = null;
        int? audioPt = null;
        for (int mi = 0; mi < sdp.Medias.Count; mi++)
        {
            var media = sdp.Medias[mi];
            string control = media.GetAttributeValue("control") ?? string.Empty;
            string rtpmap = media.GetAttributeValue("rtpmap") ?? string.Empty;
            string[] split = rtpmap.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length > 0 && int.TryParse(split[0], out int payload))
            {
                if (media.MediaType == MediaType.Video)
                {
                    videoPt = payload;
                }

                if (media.MediaType == MediaType.Audio)
                {
                    audioPt = payload;
                }
            }

            Uri trackUri;
            if (!string.IsNullOrEmpty(control))
            {
                trackUri = control.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(control)
                    : new Uri(new Uri(baseForResolution, UriKind.Absolute), control);
            }
            else
            {
                trackUri = new Uri(new Uri(baseForResolution, UriKind.Absolute), "trackID=" + (mi + 1));
            }

            tracks.Add(trackUri);
        }

        bool anyControl = sdp.Medias.Any(m => m.GetAttributeValue("control") != null);
        if (!anyControl && tracks.Count == 0)
        {
            // Session-level fallback
            Uri sessionUri = new((aggregateUrl ?? baseForResolution)!);
            tracks.Add(sessionUri);
        }

        return new SdpResolutionResult
        {
            BaseUrl = baseForResolution,
            AggregateUrl = aggregateUrl,
            TrackUris = tracks,
            SdpLines = sdpLines,
            VideoPayloadType = videoPt,
            AudioPayloadType = audioPt
        };
    }

    private static string? ResolveAggregateUrl(string baseForResolution, string? sessionControl)
    {
        try
        {
            if (string.IsNullOrEmpty(sessionControl))
            {
                return null;
            }

            string ctrl = sessionControl.Trim();
            if (ctrl == "*")
            {
                return baseForResolution;
            }

            if (ctrl.StartsWith("rtsp", StringComparison.OrdinalIgnoreCase))
            {
                return ctrl;
            }

            Uri resolved = new(new Uri(baseForResolution, UriKind.Absolute), ctrl);
            return resolved.ToString();
        }
        catch
        {
            return null;
        }
    }
}
