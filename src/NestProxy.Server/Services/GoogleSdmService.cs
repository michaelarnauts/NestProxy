using System.Net.Http.Headers;

using NestProxy.Server.Data;

namespace NestProxy.Server.Services;

/// <summary>
/// Provides high-level operations for interacting with the Google Smart Device Management (SDM) API
/// on behalf of the application. Responsibilities include:
/// <list type="bullet">
/// <item><description>Synchronizing camera device metadata into the local database (names, rooms).</description></item>
/// <item><description>Generating new RTSP live stream URLs and associated stream / extension tokens.</description></item>
/// <item><description>Extending existing RTSP streams before expiry while preserving continuity.</description></item>
/// </list>
/// </summary>
public class GoogleSdmService(
IGoogleOAuthService oauthService,
IGoogleSettingsService settingsService,
ICameraRepository cameraRepository,
ICameraIdProvider idProvider,
ILogger<GoogleSdmService> logger,
IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("google");

#pragma warning disable IDE1006 // Naming Styles
    private record DeviceList(List<Device> devices);
    private record Device(string name, string type, Traits traits, List<ParentRelation> parentRelations);
    private record ParentRelation(string parent, string displayName);
    private record Traits(Info Info);
    private record Info(string customName);
    private record ExecuteResponse(ExecuteResults results);
    private record ExecuteResults(StreamUrls streamUrls, string streamExtensionToken, string expiresAt, string? streamToken);
    private record StreamUrls(string rtspUrl);
#pragma warning restore IDE1006 // Naming Styles

    private async Task<HttpClient?> PrepareClientAsync(CancellationToken ct)
    {
        string? token = await oauthService.GetValidAccessTokenAsync(ct);
        if (token == null)
        {
            return null;
        }

        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _http;
    }

    /// <summary>
    /// Synchronizes the local <see cref="CameraEntity"/> table with devices returned by the SDM API for the
    /// configured project. New compatible devices (CAMERA / DOORBELL / DISPLAY) are inserted; existing
    /// entries have their display name and room updated. Local endpoint paths are generated for newly
    /// discovered cameras lacking one (sanitized and de-duplicated).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of camera records processed (created or updated).</returns>
    public async Task<int> SyncCamerasAsync(CancellationToken ct)
    {
        HttpClient? client = await PrepareClientAsync(ct);
        if (client == null)
        {
            return 0;
        }

        GoogleAuthOptions settings = await settingsService.GetOptionsForAuthenticationAsync(ct);
        if (string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            logger.LogWarning("Cannot sync cameras: Google ProjectId is not configured.");
            return 0;
        }

        string url = $"https://smartdevicemanagement.googleapis.com/v1/enterprises/{settings.ProjectId}/devices";
        HttpResponseMessage response = await client.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Failed to list devices: {Status}.", response.StatusCode);
            return 0;
        }

        DeviceList? data = await response.Content.ReadFromJsonAsync<DeviceList>(cancellationToken: ct);
        if (data?.devices == null)
        {
            return 0;
        }

        // Load existing IDs upfront to ensure uniqueness for new cameras.
        HashSet<string> existingIds = [.. await cameraRepository.GetAllIdsAsync(ct)];

        int count = 0;
        foreach (Device device in data.devices.Where(d => d.type.Contains("CAMERA") || d.type.Contains("DOORBELL") || d.type.Contains("DISPLAY")))
        {
            CameraEntity? camera = await cameraRepository.GetByDeviceNameAsync(device.name, ct);
            if (camera == null)
            {
                camera = new CameraEntity
                {
                    DeviceName = device.name,
                    DisplayName = device.traits?.Info?.customName ?? device.name,
                    Room = device.parentRelations?.FirstOrDefault()?.displayName,
                    IsEnabled = false
                };

                // Assign ID immediately before saving.
                string id = idProvider.CreateId(existingIds, camera);
                camera.Id = id;
                existingIds.Add(id);

                await cameraRepository.AddAsync(camera, ct);
            }
            else
            {
                camera.DisplayName = device.traits?.Info?.customName ?? camera.DisplayName;
                camera.Room = device.parentRelations?.FirstOrDefault()?.displayName ?? camera.Room;
                camera.UpdatedAt = DateTime.UtcNow;
            }

            count++;
        }

        await cameraRepository.SaveChangesAsync(ct);

        return count;
    }

    /// <summary>
    /// Requests a new live RTSP stream for the specified camera via the SDM API.
    /// </summary>
    /// <param name="cam">Camera entity for which to generate a stream (must contain DeviceName).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// An <see cref="RtspStreamResult"/> containing the stream URL and tokens if successful; otherwise <c>null</c>.
    /// </returns>
    public async Task<RtspStreamResult?> GenerateRtspStreamAsync(CameraEntity cam, CancellationToken ct)
    {
        HttpClient? client = await PrepareClientAsync(ct);
        if (client == null)
        {
            return null;
        }

        string url = $"https://smartdevicemanagement.googleapis.com/v1/{cam.DeviceName}:executeCommand";
        var body = new { command = "sdm.devices.commands.CameraLiveStream.GenerateRtspStream", @params = new { } };
        HttpResponseMessage response = await client.PostAsJsonAsync(url, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("GenerateRtspStream failed: {Status}.", response.StatusCode);
            return null;
        }

        ExecuteResponse? data = await response.Content.ReadFromJsonAsync<ExecuteResponse>(cancellationToken: ct);
        if (data?.results?.streamUrls?.rtspUrl == null || data.results.streamExtensionToken == null || data.results.streamToken == null)
        {
            return null;
        }

        DateTime expiresAt = DateTime.UtcNow.AddMinutes(5);
        if (DateTime.TryParse(data.results.expiresAt, out DateTime ex))
        {
            expiresAt = ex.ToUniversalTime();
        }

        return new RtspStreamResult(
            data.results.streamUrls.rtspUrl,
            data.results.streamExtensionToken,
            data.results.streamToken,
            expiresAt);
    }

    /// <summary>
    /// Extends an existing live RTSP stream using the stream extension token previously provided by the
    /// generation or prior extension call.
    /// </summary>
    /// <param name="cam">Camera entity containing the current <see cref="CameraEntity.StreamExtensionToken"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="StreamExtensionResult"/> containing updated tokens and expiration if successful; otherwise <c>null</c>.
    /// </returns>
    public async Task<StreamExtensionResult?> ExtendRtspStreamAsync(CameraEntity cam, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cam.StreamExtensionToken))
        {
            return null;
        }

        HttpClient? client = await PrepareClientAsync(ct);
        if (client == null)
        {
            return null;
        }

        string url = $"https://smartdevicemanagement.googleapis.com/v1/{cam.DeviceName}:executeCommand";
        var body = new { command = "sdm.devices.commands.CameraLiveStream.ExtendRtspStream", @params = new { streamExtensionToken = cam.StreamExtensionToken } };
        HttpResponseMessage response = await client.PostAsJsonAsync(url, body, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("ExtendRtspStream failed: {Status}.", response.StatusCode);
            return null;
        }

        ExecuteResponse? data = await response.Content.ReadFromJsonAsync<ExecuteResponse>(cancellationToken: ct);
        if (data?.results?.streamExtensionToken == null || data.results.streamToken == null)
        {
            return null;
        }

        DateTime expiresAt = DateTime.UtcNow.AddMinutes(5);
        if (DateTime.TryParse(data.results.expiresAt, out DateTime ex))
        {
            expiresAt = ex.ToUniversalTime();
        }

        return new StreamExtensionResult(
            data.results.streamToken,
            data.results.streamExtensionToken,
            expiresAt);
    }
}