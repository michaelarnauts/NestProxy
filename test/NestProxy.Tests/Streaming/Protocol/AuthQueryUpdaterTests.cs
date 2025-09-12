using NestProxy.Server.Streaming.Protocol;
using Xunit;

namespace NestProxy.Tests.Streaming.Protocol;

public class AuthQueryUpdaterTests
{
    [Fact]
    public void UpdateAuth_ReplacesAuthQueryParam()
    {
        string url = "rtsp://example.com/track?auth=oldtoken&foo=bar";
        string newAuth = "newtoken";
        string updated = AuthQueryUpdater.UpdateAuth(url, newAuth);
        Assert.Contains("auth=newtoken", updated);
        Assert.DoesNotContain("auth=oldtoken", updated);
        Assert.Contains("foo=bar", updated);
    }

    [Fact]
    public void UpdateAuth_AddsAuthIfMissing()
    {
        string url = "rtsp://example.com/track?foo=bar";
        string newAuth = "token";
        string updated = AuthQueryUpdater.UpdateAuth(url, newAuth);
        Assert.Contains("auth=token", updated);
        Assert.Contains("foo=bar", updated);
    }
}
