using System.Net.Sockets;

namespace NestProxy.Server.Streaming.Forwarding;

/// <inheritdoc />
public sealed class StreamForwarder(
    CameraRegistry cameras,
    ISessionRegistry sessions,
    BindingSelector bindingSelector,
    IStatisticsReporter statistics,
    IRtpPacketParser rtpPacketParser,
    ILogger<StreamForwarder> logger) : IStreamForwarder
{
    /// <inheritdoc />
    public async ValueTask ForwardRtpAsync(string cameraPath, ReadOnlyMemory<byte> rtpPacket, CancellationToken cancellationToken = default)
    {
        if (!rtpPacketParser.IsValidRtp(rtpPacket.Span) || !cameras.Exists(cameraPath))
        {
            return;
        }

        uint? ssrc = rtpPacketParser.GetSsrc(rtpPacket.Span, isRtp: true);
        int? payloadType = rtpPacketParser.GetPayloadType(rtpPacket.Span);
        if (ssrc is null || payloadType is null)
        {
            return;
        }

        var snapshot = sessions.GetSessionsSnapshot(cameraPath);
        foreach (ClientSession session in snapshot)
        {
            if (!session.IsPlaying)
            {
                continue;
            }

            try
            {
                TrackBinding? existing = bindingSelector.TryGetBindingBySsrc(cameraPath, session.SessionId, ssrc.Value);
                if (existing != null)
                {
                    await WriteRtpAsync(existing, rtpPacket).ConfigureAwait(false);
                    statistics.Increment(cameraPath, session.SessionId, payloadType.Value, true);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    continue;
                }

                bindingSelector.RememberPayloadType(cameraPath, session.SessionId, ssrc.Value, payloadType.Value);
                if (session.BindingsByPayload.TryGetValue(payloadType.Value, out List<TrackBinding>? bindings) && bindings.Count > 0)
                {
                    TrackBinding? chosen = bindingSelector.GetOrChooseBinding(cameraPath, session.SessionId, ssrc.Value, bindings);
                    if (chosen != null)
                    {
                        await WriteRtpAsync(chosen, rtpPacket).ConfigureAwait(false);
                        statistics.Increment(cameraPath, session.SessionId, payloadType.Value, true);
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        continue;
                    }
                }

                await WriteSessionRtpAsync(session, rtpPacket).ConfigureAwait(false);
                statistics.Increment(cameraPath, session.SessionId, payloadType.Value, true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RTP forward failure for camera {Camera} session {Session}", cameraPath, session.SessionId);
                RemoveSession(session);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask ForwardRtcpAsync(string cameraPath, ReadOnlyMemory<byte> rtcpPacket, CancellationToken cancellationToken = default)
    {
        if (!rtpPacketParser.IsValidRtcp(rtcpPacket.Span) || !cameras.Exists(cameraPath))
        {
            return;
        }

        byte? packetType = rtpPacketParser.GetRtcpPacketType(rtcpPacket.Span);
        uint? ssrc = null;
        if (packetType is 200 or 201 or 202 or 203)
        {
            ssrc = rtpPacketParser.GetSsrc(rtcpPacket.Span, isRtp: false);
        }

        var snapshot = sessions.GetSessionsSnapshot(cameraPath);
        foreach (ClientSession session in snapshot)
        {
            if (!session.IsPlaying)
            {
                continue;
            }

            try
            {
                TrackBinding? binding = null;
                int? payloadType = null;
                if (ssrc != null)
                {
                    binding = bindingSelector.TryGetBindingBySsrc(cameraPath, session.SessionId, ssrc.Value);
                    if (binding == null)
                    {
                        payloadType = bindingSelector.TryGetPayloadType(cameraPath, session.SessionId, ssrc.Value);
                        if (payloadType != null && session.BindingsByPayload.TryGetValue(payloadType.Value, out List<TrackBinding>? list) && list.Count > 0)
                        {
                            binding = list[0];
                        }
                    }
                }

                if (binding != null)
                {
                    await WriteRtcpAsync(binding, rtcpPacket).ConfigureAwait(false);
                    statistics.Increment(cameraPath, session.SessionId, payloadType ?? -1, false);
                }
                else
                {
                    await WriteSessionRtcpAsync(session, rtcpPacket).ConfigureAwait(false);
                    statistics.Increment(cameraPath, session.SessionId, -1, false);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "RTCP forward failure for camera {Camera} session {Session}", cameraPath, session.SessionId);
                RemoveSession(session);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private static async ValueTask WriteRtpAsync(TrackBinding binding, ReadOnlyMemory<byte> packet)
    {
        if (binding.UseUdp && binding.UdpRtpSocket != null && binding.ClientRtpEndpoint != null)
        {
            binding.UdpRtpSocket.SendTo(packet.Span, SocketFlags.None, binding.ClientRtpEndpoint);
        }
        else if (binding.Transport != null)
        {
            await binding.Transport.WriteToDataPortAsync(packet).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteSessionRtpAsync(ClientSession session, ReadOnlyMemory<byte> packet)
    {
        if (session.UseUdp && session.UdpRtpSocket != null && session.ClientRtpEndpoint != null)
        {
            session.UdpRtpSocket.SendTo(packet.Span, SocketFlags.None, session.ClientRtpEndpoint);
        }
        else if (session.Transport != null)
        {
            await session.Transport.WriteToDataPortAsync(packet).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteRtcpAsync(TrackBinding binding, ReadOnlyMemory<byte> packet)
    {
        if (binding.UseUdp && binding.UdpRtcpSocket != null && binding.ClientRtcpEndpoint != null)
        {
            binding.UdpRtcpSocket.SendTo(packet.Span, SocketFlags.None, binding.ClientRtcpEndpoint);
        }
        else if (binding.Transport != null)
        {
            await binding.Transport.WriteToControlPortAsync(packet).ConfigureAwait(false);
        }
    }

    private static async ValueTask WriteSessionRtcpAsync(ClientSession session, ReadOnlyMemory<byte> packet)
    {
        if (session.UseUdp && session.UdpRtcpSocket != null && session.ClientRtcpEndpoint != null)
        {
            session.UdpRtcpSocket.SendTo(packet.Span, SocketFlags.None, session.ClientRtcpEndpoint);
        }
        else if (session.Transport != null)
        {
            await session.Transport.WriteToControlPortAsync(packet).ConfigureAwait(false);
        }
    }

    private static void RemoveSession(ClientSession _) { }
}