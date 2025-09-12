using NestProxy.Rtsp.Messages;
using Xunit;

namespace NestProxy.Rtsp.Tests.Rtsp.Messages;

public class PortCoupleTests
{
    [Fact]
    public void Constructor_WithValidPorts_CreatesPair()
    {
        var couple = new PortCouple(8000, 8001);

        Assert.Equal(8000, couple.First);
        Assert.Equal(8001, couple.Second);
    }

    [Fact]
    public void ToString_ReturnsCorrectFormat()
    {
        var couple = new PortCouple(8000, 8001);

        var result = couple.ToString();

        Assert.Equal("8000-8001", result);
    }

    [Theory]
    [InlineData("8000-8001", 8000, 8001)]
    [InlineData("0-65535", 0, 65535)]
    [InlineData("1234-5678", 1234, 5678)]
    public void Parse_WithValidString_ReturnsCorrectPair(string input, int expectedFirst, int expectedSecond)
    {
        var couple = PortCouple.Parse(input);

        Assert.Equal(expectedFirst, couple.First);
        Assert.Equal(expectedSecond, couple.Second);
    }

    [Theory]
    [InlineData("8000")]
    [InlineData("8000-8001-8002")]
    [InlineData("")]
    [InlineData("abc-def")]
    public void Parse_WithInvalidString_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => PortCouple.Parse(input));
    }

    [Theory]
    [InlineData("8000-8001", true, 8000, 8001)]
    [InlineData("1-2", true, 1, 2)]
    [InlineData("8000", false, 0, 0)]
    [InlineData("abc-def", false, 0, 0)]
    [InlineData("", false, 0, 0)]
    [InlineData("8000-8001-8002", false, 0, 0)]
    public void TryParse_WithVariousInputs_ReturnsExpectedResult(string input, bool expectedSuccess, int expectedFirst, int expectedSecond)
    {
        var success = PortCouple.TryParse(input, out var result);

        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedFirst, result.First);
            Assert.Equal(expectedSecond, result.Second);
        }
    }

    [Fact]
    public void Equality_WithSameValues_ReturnsTrue()
    {
        var couple1 = new PortCouple(8000, 8001);
        var couple2 = new PortCouple(8000, 8001);

        Assert.Equal(couple1, couple2);
        Assert.True(couple1 == couple2);
    }

    [Fact]
    public void Equality_WithDifferentValues_ReturnsFalse()
    {
        var couple1 = new PortCouple(8000, 8001);
        var couple2 = new PortCouple(8002, 8003);

        Assert.NotEqual(couple1, couple2);
        Assert.True(couple1 != couple2);
    }
}
