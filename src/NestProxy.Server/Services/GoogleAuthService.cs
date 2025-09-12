using System.Text.Json.Serialization;

using NestProxy.Server.Data;

namespace NestProxy.Server.Services;

/// <summary>
/// Handles OAuth2 authorization code and refresh token flows for Google SDM API access.
/// </summary>
/// <remarks>
/// This service focuses solely on OAuth authentication operations. Configuration management
/// is delegated to <see cref="IGoogleSettingsService"/>.
/// </remarks>
public class GoogleAuthService(IGoogleSettingsService settingsService, FileCredentialsStore credentialsStore, ILogger<GoogleAuthService> logger, IHttpClientFactory httpClientFactory) : IGoogleOAuthService
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string AuthEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string Scope = "https://www.googleapis.com/auth/sdm.service";

    private readonly HttpClient _http = httpClientFactory.CreateClient("google");

    /// <inheritdoc />
    public async Task<string> BuildAuthorizationUrlAsync(string state, CancellationToken cancellationToken = default)
    {
        GoogleAuthOptions options = await settingsService.GetOptionsForAuthenticationAsync(cancellationToken);

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = options.RedirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",
            ["prompt"] = "consent",
            ["state"] = state
        };

        return AuthEndpoint + "?" + string.Join("&", parameters.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value ?? string.Empty)));
    }

    /// <inheritdoc />
    public async Task<Credentials> ExchangeCodeForTokensAsync(string code, CancellationToken cancellationToken = default)
    {
        GoogleAuthOptions options = await settingsService.GetOptionsForAuthenticationAsync(cancellationToken);

        var body = new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["redirect_uri"] = options.RedirectUri,
            ["grant_type"] = "authorization_code"
        };

        HttpResponseMessage response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(body), cancellationToken);
        response.EnsureSuccessStatusCode();

        TokenResponse? token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken)
            ?? throw new InvalidOperationException("Token parse failed");

        Credentials credentials = await credentialsStore.GetAsync(cancellationToken) ?? new Credentials();

        credentials.ProjectId = options.ProjectId;
        credentials.RefreshToken = token.RefreshToken ?? credentials.RefreshToken;
        credentials.AccessToken = token.AccessToken;
        credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);

        await credentialsStore.SaveAsync(credentials, cancellationToken);

        return credentials;
    }

    /// <inheritdoc />
    public async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        Credentials? credentials = await credentialsStore.GetAsync(cancellationToken);
        if (credentials == null)
        {
            return null;
        }

        // Check if token is expired or about to expire (within 1 minute)
        if (!credentials.ExpiresAt.HasValue || credentials.ExpiresAt < DateTime.UtcNow.AddMinutes(1))
        {
            if (string.IsNullOrWhiteSpace(credentials.RefreshToken))
            {
                return null;
            }

            logger.LogInformation("Refreshing Google access token.");

            GoogleAuthOptions options = await settingsService.GetOptionsForAuthenticationAsync(cancellationToken);

            var body = new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["refresh_token"] = credentials.RefreshToken,
                ["grant_type"] = "refresh_token"
            };

            HttpResponseMessage response = await _http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(body), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Refresh token failed: {Status}.", response.StatusCode);
                return null;
            }

            TokenResponse? token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken: cancellationToken);
            if (token == null)
            {
                return null;
            }

            credentials.AccessToken = token.AccessToken;
            credentials.ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresIn);
            await credentialsStore.SaveAsync(credentials, cancellationToken);
        }

        return credentials.AccessToken;
    }

    private record TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }
}