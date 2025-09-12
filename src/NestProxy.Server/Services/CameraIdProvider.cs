using System.Text.RegularExpressions;

using NestProxy.Server.Data;

namespace NestProxy.Server.Services;

/// <summary>
/// Provides camera identifier generation for camera devices.
/// Sanitizes camera names and ensures uniqueness across the camera collection.
/// </summary>
public partial class CameraIdProvider : ICameraIdProvider
{
    /// <summary>
    /// Gets a regex that matches any character not in the set [a-z0-9-]. Used to filter invalid characters from device names.
    /// </summary>
    /// <returns>
    /// A compiled <see cref="Regex"/> for invalid character matching.
    /// </returns>
    [GeneratedRegex("[^a-z0-9-]", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex InvalidCharsRegex();

    /// <summary>
    /// Gets a regex that matches one or more consecutive hyphens. Used to collapse multiple hyphens into one.
    /// </summary>
    /// <returns>
    /// A compiled <see cref="Regex"/> for matching multiple hyphens.
    /// </returns>
    [GeneratedRegex("-+", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex MultiHyphenRegex();

    /// <inheritdoc />
    public string SanitizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string sanitized = input.Trim().ToLowerInvariant().Replace(' ', '-');
        sanitized = InvalidCharsRegex().Replace(sanitized, string.Empty);
        sanitized = MultiHyphenRegex().Replace(sanitized, "-");
        sanitized = sanitized.Trim('-');
        return sanitized;
    }

    /// <inheritdoc />
    public string CreateId(HashSet<string> existingIds, CameraEntity camera)
    {
        // Prefer display name; fall back to room or device name segment if invalid.
        string source = camera.DisplayName;
        if (string.IsNullOrWhiteSpace(source) || source.Contains('/'))
        {
            source = !string.IsNullOrWhiteSpace(camera.Room)
                ? camera.Room
                : camera.DeviceName.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "camera";
        }

        // Sanitize the base name.
        string baseName = SanitizeName(source);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "camera";
        }

        // Ensure uniqueness by appending a numeric suffix if needed.
        string candidate = baseName;
        for (int suffix = 1; existingIds.Contains(candidate); suffix++)
        {
            candidate = baseName + "-" + suffix;
        }

        return candidate;
    }
}
