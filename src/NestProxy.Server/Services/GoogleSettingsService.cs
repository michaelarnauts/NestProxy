using NestProxy.Server.Data;
using NestProxy.Server.Models;

namespace NestProxy.Server.Services;

/// <summary>
/// Service for managing Google OAuth and Device Access configuration settings,
/// including persistence and retrieval with optional masking for display purposes.
/// </summary>
/// <remarks>
/// This service uses the database as the sole source of truth for Google configuration.
/// Settings must be configured through the Settings UI before authentication can occur.
/// Data access is delegated to <see cref="IGoogleSettingsRepository"/>.
/// </remarks>
public class GoogleSettingsService(IGoogleSettingsRepository repository) : IGoogleSettingsService
{
    private GoogleAuthOptions? _cachedOptions;

    /// <inheritdoc />
    public async Task<GoogleAuthOptions> GetCurrentOptionsAsync(CancellationToken cancellationToken = default)
    {
        GoogleAuthOptions opts = await GetOptionsForAuthenticationAsync(cancellationToken);
        
        // Return a masked copy
        return new GoogleAuthOptions
        {
            ClientId = opts.ClientId,
            ClientSecret = string.IsNullOrEmpty(opts.ClientSecret) ? string.Empty : "***",
            ProjectId = opts.ProjectId,
            RedirectUri = opts.RedirectUri
        };
    }

    /// <inheritdoc />
    public async Task<GoogleAuthOptions> GetOptionsForAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedOptions != null)
        {
            return _cachedOptions;
        }

        // Load from database only
        try
        {
            GoogleSettings? settings = await repository.GetAsync(cancellationToken);
            if (settings != null && !string.IsNullOrWhiteSpace(settings.ClientId))
            {
                GoogleAuthOptions opts = new()
                {
                    ClientId = settings.ClientId,
                    ClientSecret = settings.ClientSecret,
                    ProjectId = settings.ProjectId,
                    RedirectUri = settings.RedirectUri
                };

                _cachedOptions = opts;
                return opts;
            }
        }
        catch
        {
            // Fall through to return empty options
        }

        // Return empty options if no database settings exist
        // This allows the app to start but requires configuration through the Settings UI
        GoogleAuthOptions emptyOpts = new()
        {
            ClientId = string.Empty,
            ClientSecret = string.Empty,
            ProjectId = string.Empty,
            RedirectUri = string.Empty
        };

        return emptyOpts;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(GoogleAuthOptions options, CancellationToken cancellationToken = default)
    {
        GoogleSettings settings = new()
        {
            ClientId = options.ClientId,
            ClientSecret = options.ClientSecret,
            ProjectId = options.ProjectId,
            RedirectUri = options.RedirectUri
        };

        // Handle masked secret - preserve existing if masked
        if (!string.IsNullOrWhiteSpace(options.ClientSecret) && options.ClientSecret == "***")
        {
            GoogleSettings? existing = await repository.GetAsync(cancellationToken);
            if (existing != null)
            {
                settings.ClientSecret = existing.ClientSecret;
            }
        }

        await repository.UpsertAsync(settings, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        
        // Clear cache to force reload on next access
        _cachedOptions = null;
    }
}
