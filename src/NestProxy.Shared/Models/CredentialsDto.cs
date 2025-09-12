namespace NestProxy.Shared.Models;

/// <summary>
/// Represents a Google API credentials for accessing Nest Device Access resources.
/// </summary>
public class CredentialsDto
{
    /// <summary>
    /// Gets or sets the Google Cloud project ID associated with this credentials.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC expiration time for the credentials, if any.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether the credentials is currently valid (not expired).
    /// </summary>
    public bool IsValid => ExpiresAt.HasValue && ExpiresAt > DateTime.UtcNow.AddMinutes(1);
}