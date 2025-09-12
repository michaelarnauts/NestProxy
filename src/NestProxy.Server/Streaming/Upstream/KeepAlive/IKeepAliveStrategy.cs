namespace NestProxy.Server.Streaming.Upstream.KeepAlive;

/// <summary>
/// Strategy for issuing periodic upstream keep-alive traffic for an established <see cref="IUpstreamSession"/>.
/// </summary>
public interface IKeepAliveStrategy
{
    /// <summary>Executes one keep-alive iteration (fire-and-forget primitive; caller handles scheduling).</summary>
    /// <param name="session">Target upstream session.</param>
    /// <returns>True if keep-alive was sent (or attempted), false if skipped.</returns>
    bool TrySend(IUpstreamSession session);
}

public static class KeepAliveStrategyFactory
{
    public static IKeepAliveStrategy Create(KeepaliveMode mode, ILoggerFactory loggerFactory)
        => mode switch
        {
            KeepaliveMode.Options => new OptionsKeepAliveStrategy(),
            KeepaliveMode.GetParameter => new GetParameterKeepAliveStrategy(loggerFactory.CreateLogger<GetParameterKeepAliveStrategy>()),
            KeepaliveMode.Rtcp => new RtcpKeepAliveStrategy(loggerFactory.CreateLogger<RtcpKeepAliveStrategy>()),
            _ => new OptionsKeepAliveStrategy()
        };
}

public sealed class OptionsKeepAliveStrategy : IKeepAliveStrategy
{
    public bool TrySend(IUpstreamSession session)
    {
        session.SendOptionsKeepalive();
        return true;
    }
}

public sealed class GetParameterKeepAliveStrategy(ILogger<GetParameterKeepAliveStrategy> logger) : IKeepAliveStrategy
{
    private readonly ILogger<GetParameterKeepAliveStrategy> _logger = logger;
    private volatile bool _fallbackToOptions;

    public bool TrySend(IUpstreamSession session)
    {
        if (_fallbackToOptions)
        {
            session.SendOptionsKeepalive();
            return true;
        }

        try
        {
            session.SendGetParameterKeepalive();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "GET_PARAMETER keep-alive failed; falling back to OPTIONS for remainder of session.");
            _fallbackToOptions = true;
            try
            {
                session.SendOptionsKeepalive();
            }
            catch
            {
            }

            return true;
        }
    }
}

public sealed class RtcpKeepAliveStrategy(ILogger<RtcpKeepAliveStrategy> logger) : IKeepAliveStrategy
{
    private readonly ILogger<RtcpKeepAliveStrategy> _logger = logger;
    private byte[]? _cnameBytes;
    private uint? _ssrc;

    public bool TrySend(IUpstreamSession session)
    {
        if (!session.TryGetFirstControlChannel(out _))
        {
            return false; // control channel not ready yet
        }

        _cnameBytes ??= System.Text.Encoding.ASCII.GetBytes("nestproxy");
        _ssrc ??= (uint)Random.Shared.NextInt64();
        try
        {
            int needed = RtcpPacketBuilder.ComputeCompoundRrWithSdesLength(_cnameBytes.Length);
            Span<byte> stack = needed <= 1500 ? stackalloc byte[needed] : new byte[needed];
            if (RtcpPacketBuilder.TryWriteCompoundRrWithSdes(stack, _ssrc.Value, _cnameBytes, out int written))
            {
                session.SendRtcpKeepalive(stack.Slice(0, written).ToArray(), written); // session expects array
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RTCP keep-alive attempt failed");
        }

        return false;
    }
}