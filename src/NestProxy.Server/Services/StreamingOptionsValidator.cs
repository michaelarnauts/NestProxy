using Microsoft.Extensions.Options;

namespace NestProxy.Server.Services;

/// <summary>
/// Validates <see cref="StreamingOptions"/> values loaded from configuration to ensure they fall within safe
/// operational thresholds (e.g. port ranges and interval bounds).
/// </summary>
public sealed class StreamingOptionsValidator : IValidateOptions<StreamingOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, StreamingOptions options)
    {
        List<string> failures = [];
        if (options.RtspBasePort is < 1024 or > 65535)
        {
            failures.Add("RtspBasePort must be between 1024 and 65535.");
        }

        if (options.SessionTimeoutSeconds <= 0 || options.SessionTimeoutSeconds > 3600)
        {
            failures.Add("SessionTimeoutSeconds must be 1-3600.");
        }

        if (options.StatsWindowSeconds <= 0 || options.StatsWindowSeconds > 300)
        {
            failures.Add("StatsWindowSeconds must be 1-300.");
        }

        if (options.KeepaliveIntervalSeconds < 5 || options.KeepaliveIntervalSeconds > 300)
        {
            failures.Add("KeepaliveIntervalSeconds must be 5-300.");
        }

        if (options.StreamExtensionOffsetSeconds < 10 || options.StreamExtensionOffsetSeconds > 300)
        {
            failures.Add("StreamExtensionOffsetSeconds must be 10-300.");
        }

        if (options.PacketChannelCapacity < 16 || options.PacketChannelCapacity > 10000)
        {
            failures.Add("PacketChannelCapacity must be 16-10000.");
        }

        return failures.Count > 0 ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
    }
}