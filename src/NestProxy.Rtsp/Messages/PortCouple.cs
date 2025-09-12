namespace NestProxy.Rtsp.Messages;

/// <summary>
/// Represents a pair of port numbers (typically for RTP data and RTCP control).
/// </summary>
public readonly record struct PortCouple(int First, int Second)
{
    /// <inheritdoc />
    public override string ToString() => $"{First}-{Second}";

    /// <summary>
    /// Parses a string representation of a port couple into a new PortCouple instance.
    /// </summary>
    /// <param name="value">The string to parse, in the format "X-Y", where X and Y are integer port numbers.</param>
    /// <returns>A PortCouple instance representing the ports specified in the input string.</returns>
    /// <exception cref="FormatException">
    /// Thrown if the input string is not in the correct format or cannot be parsed as two integer port numbers
    /// separated by a hyphen.
    /// </exception>
    public static PortCouple Parse(string value)
    {
        var parts = value.Split('-');
        return parts.Length == 2
            ? new PortCouple(int.Parse(parts[0]), int.Parse(parts[1]))
            : throw new FormatException($"Invalid port couple format: {value}");
    }

    /// <summary>
    /// Attempts to parse the specified string into a PortCouple instance.
    /// </summary>
    /// <remarks>
    /// The input string must be in the format "number-number", where both parts are valid integers.
    /// No exceptions are thrown if parsing fails.
    /// </remarks>
    /// <param name="value">The string representation of a port couple in the format "number-number" to parse.</param>
    /// <param name="result">
    /// When this method returns, contains the PortCouple value equivalent to the parsed string if the conversion
    /// succeeded, or the default value if the conversion failed.
    /// </param>
    /// <returns>true if the string was successfully parsed; otherwise, false.</returns>
    public static bool TryParse(string value, out PortCouple result)
    {
        result = default;
        var parts = value.Split('-');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var first) || !int.TryParse(parts[1], out var second))
        {
            return false;
        }

        result = new PortCouple(first, second);
        return true;
    }
}
