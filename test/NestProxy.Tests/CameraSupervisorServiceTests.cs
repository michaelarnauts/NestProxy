using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using NestProxy.Server.Configuration;
using NestProxy.Server.Core;
using NestProxy.Server.Data;
using NestProxy.Server.Models;

using Xunit;

namespace NestProxy.Tests;

public class CameraSupervisorServiceTests
{
    [Fact]
    public async Task Start_And_Stop_NoCameras_DoesNotThrow()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions();
            services.Configure<GoogleAuthOptions>(_ => { });
            services.Configure<ConfigStorageOptions>(o => o.RootPath = root);
            services.AddSingleton<ICameraRepository, FileCameraRepository>();
            var inMemory = new Dictionary<string, string?>();
            IConfiguration config = new ConfigurationBuilder().AddInMemoryCollection(inMemory!).Build();
            services.AddSingleton(config);
            services.AddSingleton<IHostedService, CameraSupervisorService>();
            var provider = services.BuildServiceProvider();
            var hosted = provider.GetRequiredService<IHostedService>();
            await hosted.StartAsync(CancellationToken.None);
            await Task.Delay(50, TestContext.Current.CancellationToken);
            await hosted.StopAsync(CancellationToken.None);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}