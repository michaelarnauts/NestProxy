using NestProxy.Server.Streaming.Forwarding.Binding;

using Xunit;

namespace NestProxy.Tests.Services;

public class BindingSelectorSsrcSwitchTests
{
    [Fact]
    public void ClearCamera_RemovesAllSsrcBindings()
    {
        // Arrange
        var selector = new BindingSelector();
        var binding1 = new TrackBinding();
        var binding2 = new TrackBinding();

        // Associate SSRCs for camera "cam-42" session "sess-1"
        selector.AssociateSsrc("cam-42", "sess-1", 0x11111111, binding1, 97);
        selector.AssociateSsrc("cam-42", "sess-1", 0x22222222, binding2, 98);

        // Associate SSRCs for camera "cam-99" session "sess-2" (different camera)
        selector.AssociateSsrc("cam-99", "sess-2", 0x33333333, binding1, 97);

        // Verify initial associations exist
        Assert.Same(binding1, selector.TryGetBindingBySsrc("cam-42", "sess-1", 0x11111111));
        Assert.Same(binding2, selector.TryGetBindingBySsrc("cam-42", "sess-1", 0x22222222));
        Assert.Same(binding1, selector.TryGetBindingBySsrc("cam-99", "sess-2", 0x33333333));

        // Act: Clear camera "cam-42"
        selector.ClearCamera("cam-42");

        // Assert: cam-42 bindings are gone, cam-99 bindings remain
        Assert.Null(selector.TryGetBindingBySsrc("cam-42", "sess-1", 0x11111111));
        Assert.Null(selector.TryGetBindingBySsrc("cam-42", "sess-1", 0x22222222));
        Assert.Same(binding1, selector.TryGetBindingBySsrc("cam-99", "sess-2", 0x33333333));

        // Assert: Payload types also cleared for cam-42
        Assert.Null(selector.TryGetPayloadType("cam-42", "sess-1", 0x11111111));
        Assert.Null(selector.TryGetPayloadType("cam-42", "sess-1", 0x22222222));
        Assert.Equal(97, selector.TryGetPayloadType("cam-99", "sess-2", 0x33333333));
    }

    [Fact]
    public void GetOrChooseBinding_ReassociatesAfterClear()
    {
        // Arrange
        var selector = new BindingSelector();
        var binding = new TrackBinding();
        var bindings = new List<TrackBinding> { binding };

        // Initial association with old SSRC
        selector.AssociateSsrc("cam-42", "sess-1", 0xAABBCCDD, binding, 97);
        Assert.Same(binding, selector.TryGetBindingBySsrc("cam-42", "sess-1", 0xAABBCCDD));

        // Act: Simulate upstream session switch
        selector.ClearCamera("cam-42");

        // Assert: Old SSRC is no longer associated
        Assert.Null(selector.TryGetBindingBySsrc("cam-42", "sess-1", 0xAABBCCDD));

        // Assert: New SSRC can be associated to the same binding
        var chosen = selector.GetOrChooseBinding("cam-42", "sess-1", 0x11223344, bindings);
        Assert.Same(binding, chosen);
        Assert.Same(binding, selector.TryGetBindingBySsrc("cam-42", "sess-1", 0x11223344));
    }

    [Fact]
    public void ClearCamera_HandlesNonExistentCamera()
    {
        // Arrange
        var selector = new BindingSelector();

        // Act & Assert: Should not throw
        selector.ClearCamera("cam-nonexistent");
    }

    [Fact]
    public void ClearCamera_HandlesMultipleSessions()
    {
        // Arrange
        var selector = new BindingSelector();
        var binding1 = new TrackBinding();
        var binding2 = new TrackBinding();

        // Same camera, different sessions
        selector.AssociateSsrc("cam-42", "sess-A", 0xAAAAAAAA, binding1, 97);
        selector.AssociateSsrc("cam-42", "sess-B", 0xBBBBBBBB, binding2, 97);

        // Act
        selector.ClearCamera("cam-42");

        // Assert: Both sessions' bindings cleared
        Assert.Null(selector.TryGetBindingBySsrc("cam-42", "sess-A", 0xAAAAAAAA));
        Assert.Null(selector.TryGetBindingBySsrc("cam-42", "sess-B", 0xBBBBBBBB));
    }
}
