using System.Net;

using NestProxy.Rtsp.Messages;
using NestProxy.Rtsp.Server;
using NestProxy.Rtsp.Transport;

namespace NestProxy.Server.Streaming.Downstream.Server.Transport;

/// <summary>
/// Service for handling downstream RTSP SETUP requests: selects a transport, applies/creates track bindings
/// and populates the response headers. Keeps transport negotiation logic out of <see cref="LocalRtspServer"/>.
/// </summary>
public sealed class TransportSetupService(
    ITransportAllocator allocator,
    ITransportNegotiator negotiator,
    ILogger<TransportSetupService> logger) : ITransportSetupService
{
    private readonly ITransportAllocator _allocator = allocator;
    private readonly ILogger<TransportSetupService> _logger = logger;
    private readonly ITransportNegotiator _negotiator = negotiator;

    /// <inheritdoc />
    public async Task HandleSetupAsync(RtspServerConnection connection, RtspRequest setup, ClientSession session, CameraStream camera, int? payloadTypeForThisSetup, bool isSessionLevelSetup, CancellationToken cancellationToken = default)
    {
        try
        {
            RtspTransport[]? requested = null;
            if (setup.Headers.TryGetValue("Transport", out string? transportHeader) && !string.IsNullOrWhiteSpace(transportHeader))
            {
                requested = [.. transportHeader.Split(',').Select(RtspTransport.Parse)];
            }

            if (requested == null || requested.Length == 0)
            {
                RtspResponse bad = setup.CreateResponse();
                bad.StatusCode = RtspStatusCode.BadRequest;
                await connection.SendResponseAsync(bad, cancellationToken).ConfigureAwait(false);
                return;
            }

            RtspTransport? selectedTransport = _negotiator.NegotiateTransport(requested);
            if (selectedTransport == null)
            {
                RtspResponse notSupported = setup.CreateResponse();
                notSupported.StatusCode = RtspStatusCode.UnsupportedTransport;
                await connection.SendResponseAsync(notSupported, cancellationToken).ConfigureAwait(false);
                return;
            }

            bool useTcp = selectedTransport.LowerTransport.Equals("TCP", StringComparison.OrdinalIgnoreCase);
            RtspTransport replyTransport;

            List<int> payloadTypes = [];
            if (isSessionLevelSetup)
            {
                // Session-level SETUP: create bindings for all available payload types
                if (camera.VideoPayloadType > 0)
                {
                    payloadTypes.Add(camera.VideoPayloadType);
                }

                if (camera.AudioPayloadType > 0)
                {
                    payloadTypes.Add(camera.AudioPayloadType);
                }

                _logger.LogInformation("Session-level SETUP: creating bindings for payload types: {PayloadTypes}", string.Join(", ", payloadTypes));
            }
            else if (payloadTypeForThisSetup.HasValue)
            {
                // Track-specific SETUP: create binding for the specific payload type
                payloadTypes.Add(payloadTypeForThisSetup.Value);
            }

            if (useTcp)
            {
                PortCouple interleaved = selectedTransport.Interleaved ?? new PortCouple(0, 1);
                RtpTcpTransport rtpTransport = new() { DataChannel = interleaved.First, ControlChannel = interleaved.Second, Connection = connection };
                session.Transport = rtpTransport;
                session.UseUdp = false;
                session.LastActivityUtc = DateTime.UtcNow;

                foreach (int payloadType in payloadTypes)
                {
                    TrackBinding trackBinding = new() { UseUdp = false, Transport = rtpTransport, InterleavedDataChannel = interleaved.First, InterleavedControlChannel = interleaved.Second };
                    session.BindingsByPayload.AddOrUpdate(payloadType, _ => [trackBinding], (_, list) =>
                    {
                        lock (list)
                        {
                            list.Add(trackBinding);
                        }

                        return list;
                    });
                }

                replyTransport = new RtspTransport { LowerTransport = "TCP", Interleaved = interleaved };
            }
            else
            {
                UdpSocketPair? allocation = _allocator.TryAllocateUdpPair();
                if (allocation is null)
                {
                    RtspResponse fail = setup.CreateResponse();
                    fail.StatusCode = RtspStatusCode.InternalServerError;
                    await connection.SendResponseAsync(fail, cancellationToken).ConfigureAwait(false);
                    return;
                }

                if (!selectedTransport.ClientPort.HasValue)
                {
                    RtspResponse bad = setup.CreateResponse();
                    bad.StatusCode = RtspStatusCode.BadRequest;
                    await connection.SendResponseAsync(bad, cancellationToken).ConfigureAwait(false);
                    return;
                }

                PortCouple client = selectedTransport.ClientPort.Value;
                IPAddress remoteIp = IPAddress.Loopback;
                IPEndPoint clientRtpEp = new(remoteIp, client.First);
                IPEndPoint clientRtcpEp = new(remoteIp, client.Second);
                session.UseUdp = true;
                session.UdpRtpSocket = allocation.Rtp;
                session.UdpRtcpSocket = allocation.Rtcp;
                session.ClientRtpEndpoint = clientRtpEp;
                session.ClientRtcpEndpoint = clientRtcpEp;
                session.ServerRtpPort = allocation.RtpPort;
                session.ServerRtcpPort = allocation.RtcpPort;
                session.LastActivityUtc = DateTime.UtcNow;

                foreach (int payloadType in payloadTypes)
                {
                    TrackBinding trackBinding = new() { UseUdp = true, UdpRtpSocket = allocation.Rtp, UdpRtcpSocket = allocation.Rtcp, ClientRtpEndpoint = clientRtpEp, ClientRtcpEndpoint = clientRtcpEp, ServerRtpPort = allocation.RtpPort, ServerRtcpPort = allocation.RtcpPort };
                    session.BindingsByPayload.AddOrUpdate(payloadType, _ => [trackBinding], (_, list) =>
                    {
                        lock (list)
                        {
                            list.Add(trackBinding);
                        }

                        return list;
                    });
                }

                replyTransport = new RtspTransport { LowerTransport = "UDP", ClientPort = new PortCouple(client.First, client.Second), ServerPort = new PortCouple(allocation.RtpPort, allocation.RtcpPort) };
            }

            RtspResponse response = setup.CreateResponse();
            response.Headers["Session"] = session.SessionId;
            response.Headers["Transport"] = replyTransport.ToString();
            await connection.SendResponseAsync(response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SETUP handling failure");
            Safe.Invoke(() =>
            {
                RtspResponse err = setup.CreateResponse();
                err.StatusCode = RtspStatusCode.InternalServerError;
                _ = connection.SendResponseAsync(err, cancellationToken);
            });
        }
    }
}