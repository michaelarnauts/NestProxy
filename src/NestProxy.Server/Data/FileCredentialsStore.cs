using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using NestProxy.Server.Configuration;

namespace NestProxy.Server.Data;

/// <summary>
/// File-based credentials store for Google OAuth tokens.
/// </summary>
public class FileCredentialsStore
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private Credentials? _cached;

    /// <summary>
    /// Initializes a new instance of the FileCredentialsStore class using the specified configuration options.
    /// </summary>
    /// <remarks>
    /// The constructor creates the credentials storage directory if it does not already exist and
    /// prepares the credentials file for use. The credentials are loaded and cached upon initialization.
    /// </remarks>
    /// <param name="options">The configuration options that specify the root path for storing credentials. Cannot be null.</param>
    public FileCredentialsStore(IOptions<ConfigStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string root = options.Value.RootPath;
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "credentials.json");
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _cached = Load();
    }

    /// <summary>
    /// Asynchronously retrieves the cached credentials, if available.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe and will wait for any ongoing credentials update to complete before returning the cached value.
    /// </remarks>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the cached credentials if available;
    /// otherwise, <see langword="null"/>.</returns>
    public async Task<Credentials?> GetAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);

        try
        {
            return _cached;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <summary>
    /// Asynchronously saves the specified credentials to persistent storage, overwriting any existing data.
    /// </summary>
    /// <remarks>
    /// The method is thread-safe and ensures atomic file replacement to prevent data corruption.
    /// </remarks>
    /// <param name="credentials">The credentials to be saved. Cannot be null.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the save operation.</param>
    /// <returns>A task that represents the asynchronous save operation.</returns>
    public async Task SaveAsync(Credentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        await _sync.WaitAsync(cancellationToken);

        try
        {
            credentials.Provider = string.IsNullOrWhiteSpace(credentials.Provider) ? "GoogleNest" : credentials.Provider;

            string tempFile = _filePath + ".tmp";
            await using (FileStream stream = new(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, credentials, _serializerOptions, cancellationToken);
            }

            File.Move(tempFile, _filePath, true);
            _cached = credentials;
        }
        finally
        {
            _sync.Release();
        }
    }

    private Credentials? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            Credentials? model = JsonSerializer.Deserialize<Credentials>(json, _serializerOptions);
            return model;
        }
        catch
        {
            return null;
        }
    }
}
