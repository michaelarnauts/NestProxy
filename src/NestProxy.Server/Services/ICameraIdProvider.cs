using NestProxy.Server.Data;

namespace NestProxy.Server.Services;

/// <summary>
/// Defines a contract for generating unique, sanitized camera identifiers and for producing valid ID
/// components from arbitrary input strings.
/// </summary>
public interface ICameraIdProvider
{
    /// <summary>
    /// Creates a unique camera identifier by sanitizing its display name, room, or device name,
    /// and appending a numeric suffix if necessary to avoid collisions with existing IDs.
    /// </summary>
    /// <param name="existingIds">
    /// A set of already-assigned camera IDs to check for uniqueness.
    /// </param>
    /// <param name="camera">
    /// The camera entity for which to generate an ID.
    /// </param>
    /// <returns>
    /// A unique sanitized ID string suitable for use as a camera identifier.
    /// </returns>
    string CreateId(HashSet<string> existingIds, CameraEntity camera);

    /// <summary>
    /// Sanitizes an input string to produce a valid camera ID component.
    /// Converts to lowercase, replaces spaces with underscores, removes invalid characters,
    /// collapses multiple underscores, and trims leading/trailing underscores.
    /// </summary>
    /// <param name="input">
    /// The raw input string to sanitize.
    /// </param>
    /// <returns>
    /// A sanitized string containing only [a-z0-9_], or an empty string if input is null/whitespace or results in an empty string after sanitization.
    /// </returns>
    string SanitizeName(string? input);
}