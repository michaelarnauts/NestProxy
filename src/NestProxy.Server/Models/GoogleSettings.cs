namespace NestProxy.Server.Models;

/// <summary>
/// Represents configuration settings required for authenticating with Google services using OAuth2.
/// </summary>
/// <remarks>
/// This class encapsulates the credentials and identifiers necessary for Google OAuth2 authentication
/// flows, including client ID, client secret, project ID, and redirect URI.
/// </remarks>
public class GoogleSettings
{
    /// <summary>
    /// The unique client identifier for the Google OAuth2 application.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// The client secret associated with the Google OAuth2 application.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// The Google Cloud project identifier.
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The redirect URI registered for the Google OAuth2 application.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;
}