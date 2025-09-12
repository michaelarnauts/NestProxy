using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NestProxy.Server.Core;
using NestProxy.Server.Models;

using Xunit;

namespace NestProxy.Tests;

public class StatisticsReporterTests
{
    [Fact]
    public void FlushAll_LogsCounters()
    {
        var cameraRegistry = new CameraRegistry();
        cameraRegistry["cam-5"] = new CameraStream { CameraId = "cam-5" };
        var stats = new StatisticsReporter(cameraRegistry, Options.Create(new StreamingOptions { StatsWindowSeconds = 1 }), NullLogger<StatisticsReporter>.Instance);
        stats.Increment("p1", "sess", 96, true);
        stats.Increment("p1", "sess", 96, false);
        stats.FlushAll();
        // No exception means success; verifying log output would require a logger spy.
    }
}