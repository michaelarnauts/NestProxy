using System.Collections.Concurrent;

using NestProxy.Server.Streaming.Forwarding.Binding;

using Xunit;

namespace NestProxy.Tests;

public class BindingSelectorConcurrencyTests
{
    [Fact]
    public void ConcurrentDeferredAssociationRemainsNullUntilAssociated()
    {
        BindingSelector selector = new();
        List<TrackBinding> bindings = [new TrackBinding(), new TrackBinding(), new TrackBinding()];
        string path = "camX";
        string session = "sessX";
        const int payloadType = 98;
        const int ssrcCount = 64;
        ConcurrentBag<TrackBinding?> initial = [];
        Parallel.For(0, ssrcCount, i =>
        {
            uint ssrc = (uint)i + 1000u;
            TrackBinding? b = selector.GetOrChooseBinding(path, session, ssrc, bindings);
            initial.Add(b);
        });
        // All should be null because multiple bindings exist for payloadType
        Assert.All(initial, b => Assert.Null(b));

        // Now associate half of the SSRCs to a fixed binding and verify retrieval becomes stable
        TrackBinding target = bindings[1];
        for (int i = 0; i < ssrcCount / 2; i++)
        {
            uint ssrc = (uint)i + 1000u;
            selector.AssociateSsrc(path, session, ssrc, target, payloadType);
        }

        Parallel.For(0, ssrcCount / 2, i =>
        {
            uint ssrc = (uint)i + 1000u;
            TrackBinding? b = selector.GetOrChooseBinding(path, session, ssrc, bindings);
            Assert.Same(target, b);
        });
    }
}