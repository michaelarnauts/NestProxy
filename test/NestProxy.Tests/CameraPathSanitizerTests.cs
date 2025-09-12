using NestProxy.Server.Data;
using NestProxy.Server.Services;

using Xunit;

namespace NestProxy.Tests;

public class CameraPathSanitizerTests
{
    [Fact]
    public async Task SanitizesAndUniqueifies()
    {
        // Arrange
        var idProvider = new CameraIdProvider();
        List<CameraEntity> cameras =
        [
            new CameraEntity { DeviceName = "enterprises/p/devices/1", DisplayName = "Front Yard", Id = string.Empty },
            new CameraEntity { DeviceName = "enterprises/p/devices/2", DisplayName = "Front Yard", Id = string.Empty }
        ];

        var existingIds = new HashSet<string>(cameras.Where(c => !string.IsNullOrWhiteSpace(c.Id)).Select(c => c.Id));

        // Act - assign IDs using the actual CameraIdProvider
        foreach (CameraEntity cam in cameras)
        {
            string id = idProvider.CreateId(existingIds, cam);
            cam.Id = id;
            existingIds.Add(id);
        }

        // Assert
        List<string> ids = cameras.OrderBy(c => c.DeviceName).Select(c => c.Id).ToList();
        Assert.Equal("front-yard", ids[0]);
        Assert.Equal("front-yard-1", ids[1]);
    }
}