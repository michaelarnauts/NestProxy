using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using NestProxy.Server.Configuration;

namespace NestProxy.Server.Data;

/// <summary>
/// File-based implementation of <see cref="IGoogleSettingsRepository"/> storing configuration in JSON.
/// </summary>
public class GoogleSettingsRepository : IGoogleSettingsRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private GoogleSettings? _cached;

    /// <summary>
    /// Initializes a new instance of the GoogleSettingsRepository class using the specified configuration storage
    /// options.
    /// </summary>
    /// <remarks>The constructor creates the storage directory if it does not exist and prepares the
    /// repository to read and write Google settings to a JSON file within the specified root path.</remarks>
    /// <param name="options">The configuration options that specify the root path for storing Google settings. Cannot be null.</param>
    public GoogleSettingsRepository(IOptions<ConfigStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        string root = options.Value.RootPath;
        Directory.CreateDirectory(root);
        _filePath = Path.Combine(root, "google-settings.json");
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        _cached = Load();
    }

    /// <inheritdoc />
    public async Task<GoogleSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (_cached != null)
            {
                return _cached;
            }

            _cached = Load();
            return _cached;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpsertAsync(GoogleSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        await _sync.WaitAsync(cancellationToken);

        try
        {
            await PersistAsync(settings, cancellationToken);
            _cached = settings;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // File writes are immediate in UpsertAsync; nothing additional to commit.
        await Task.CompletedTask;
        return 1;
    }

    private GoogleSettings? Load()
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        try
        {
            string json = File.ReadAllText(_filePath);
            GoogleSettings? model = JsonSerializer.Deserialize<GoogleSettings>(json, _serializerOptions);
            return model;
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistAsync(GoogleSettings settings, CancellationToken cancellationToken)
    {
        string tempFile = _filePath + ".tmp";
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await using (FileStream stream = new(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await JsonSerializer.SerializeAsync(stream, settings, _serializerOptions, cancellationToken);
        }

        File.Move(tempFile, _filePath, true);
    }
}
