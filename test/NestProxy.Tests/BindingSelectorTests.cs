using NestProxy.Server.Streaming.Forwarding.Binding;

using Xunit;

namespace NestProxy.Tests;

public class BindingSelectorTests
{
    [Fact]
    public void SingleBindingBootstrapsAssociation()
    {
        BindingSelector selector = new();
        string path = "cam";
        string session = "sess";
        uint ssrc = 123u;
        List<TrackBinding> single = [new TrackBinding()];
        TrackBinding? chosen = selector.GetOrChooseBinding(path, session, ssrc, single);
        Assert.NotNull(chosen);
        // Subsequent lookup for same SSRC returns same binding
        TrackBinding? again = selector.GetOrChooseBinding(path, session, ssrc, single);
        Assert.Same(chosen, again);
    }

    [Fact]
    public void MultipleBindingsDefersAssociation()
    {
        BindingSelector selector = new();
        List<TrackBinding> multi = [new TrackBinding(), new TrackBinding()];
        TrackBinding? none = selector.GetOrChooseBinding("p", "s", 10u, multi);
        Assert.Null(none);
        // Still null until explicit association occurs (simulate via AssociateSsrc)
        selector.AssociateSsrc("p", "s", 10u, multi[1], 97);
        TrackBinding? after = selector.GetOrChooseBinding("p", "s", 10u, multi);
        Assert.Same(multi[1], after);
    }
}