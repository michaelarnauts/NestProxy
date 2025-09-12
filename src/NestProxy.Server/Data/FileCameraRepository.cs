using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

using NestProxy.Server.Configuration;

namespace NestProxy.Server.Data;

/// <summary>
/// File-based implementation of <see cref="ICameraRepository"/> storing one JSON file per camera.
/// </summary>
public class FileCameraRepository : ICameraRepository
{
    private readonly string _camerasPath;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly List<CameraEntity> _cameras = [];

    public FileCameraRepository(IOptions<ConfigStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _camerasPath = Path.Combine(options.Value.RootPath, "cameras");
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        Directory.CreateDirectory(_camerasPath);
        LoadCameras();
    }

    /// <inheritdoc />
    public async Task<CameraEntity?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _cameras.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<CameraEntity?> GetByDeviceNameAsync(string deviceName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _cameras.FirstOrDefault(c => string.Equals(c.DeviceName, deviceName, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<CameraEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _cameras.OrderBy(c => c.DisplayName).ThenBy(c => c.Id).ToList();
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<CameraEntity>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _cameras.Where(c => c.IsEnabled).ToList();
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<List<string>> GetAllIdsAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _cameras.Select(c => c.Id).ToList();
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task<bool> AnyAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            return _cameras.Count > 0;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task AddAsync(CameraEntity camera, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentException.ThrowIfNullOrWhiteSpace(camera.Id, nameof(camera));
        await _sync.WaitAsync(cancellationToken);

        try
        {
            _cameras.Add(camera);
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CameraEntity camera, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(camera);
        await _sync.WaitAsync(cancellationToken);
        _sync.Release();
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            ValidateUnique();
            foreach (CameraEntity camera in _cameras)
            {
                string fileName = GetFilePath(camera);
                string tempFile = fileName + ".tmp";
                await using (FileStream stream = new(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                {
                    await JsonSerializer.SerializeAsync(stream, camera, _serializerOptions, cancellationToken);
                }
                File.Move(tempFile, fileName, true);
            }

            return _cameras.Count;
        }
        finally
        {
            _sync.Release();
        }
    }

    private void LoadCameras()
    {
        foreach (string file in Directory.EnumerateFiles(_camerasPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                string json = File.ReadAllText(file);
                CameraEntity? camera = JsonSerializer.Deserialize<CameraEntity>(json, _serializerOptions);
                if (camera != null && !string.IsNullOrWhiteSpace(camera.Id))
                {
                    _cameras.Add(camera);
                }
            }
            catch
            {
                // Ignore invalid files but continue loading other cameras.
            }
        }
    }

    private void ValidateUnique()
    {
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> deviceNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (CameraEntity camera in _cameras)
        {
            if (!ids.Add(camera.Id))
            {
                throw new InvalidOperationException($"Duplicate camera ID detected: {camera.Id}");
            }

            if (!deviceNames.Add(camera.DeviceName))
            {
                throw new InvalidOperationException($"Duplicate deviceName detected: {camera.DeviceName}");
            }
        }
    }

    private string GetFilePath(CameraEntity camera)
    {
        return Path.Combine(_camerasPath, camera.Id + ".json");
    }
}
