using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using NestProxy.Server.Configuration;
using NestProxy.Server.Data;
using NestProxy.Server.Models;
using NestProxy.Server.Services;

using Xunit;

namespace NestProxy.Tests;

public class GoogleAuthServiceTests
{
    private static (string Root, FileCredentialsStore CredentialsStore, GoogleSettingsRepository SettingsRepository) CreateStores()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var options = Options.Create(new ConfigStorageOptions { RootPath = root });
        var credStore = new FileCredentialsStore(options);
        var settingsRepo = new GoogleSettingsRepository(options);
        return (root, credStore, settingsRepo);
    }

    [Fact]
    public async Task BuildAuthorizationUrl_Includes_State_And_Redirect()
    {
        var (root, credStore, settingsRepo) = CreateStores();
        try
        {
            // Setup mock settings service
            var mockSettingsService = new Mock<IGoogleSettingsService>();
            mockSettingsService
                .Setup(s => s.GetOptionsForAuthenticationAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GoogleAuthOptions
                {
                    ClientId = "cid",
                    RedirectUri = "https://r",
                    ClientSecret = "secret",
                    ProjectId = "proj"
                });

            var svc = new GoogleAuthService(
                mockSettingsService.Object,
                credStore,
                NullLogger<GoogleAuthService>.Instance,
                new DummyHttpClientFactory());

            var url = await svc.BuildAuthorizationUrlAsync("abc123", TestContext.Current.CancellationToken);

            Assert.Contains("client_id=cid", url);
            Assert.Contains("redirect_uri=https%3A%2F%2Fr", url);
            Assert.Contains("state=abc123", url);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task GoogleSettingsService_Masks_ClientSecret()
    {
        var (root, credStore, settingsRepo) = CreateStores();
        try
        {
            // Seed settings with a secret
            var settings = new GoogleSettings
            {
                ClientId = "test-client",
                ClientSecret = "super-secret",
                ProjectId = "test-project",
                RedirectUri = "https://test"
            };

            await settingsRepo.UpsertAsync(settings, TestContext.Current.CancellationToken);

            var settingsService = new GoogleSettingsService(settingsRepo);

            var masked = await settingsService.GetCurrentOptionsAsync(TestContext.Current.CancellationToken);

            Assert.Equal("test-client", masked.ClientId);
            Assert.Equal("***", masked.ClientSecret);
            Assert.Equal("test-project", masked.ProjectId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task GoogleSettingsService_DoesNotMask_ForAuthentication()
    {
        var (root, credStore, settingsRepo) = CreateStores();
        try
        {
            // Seed settings
            var settings = new GoogleSettings
            {
                ClientId = "test-client",
                ClientSecret = "super-secret",
                ProjectId = "test-project",
                RedirectUri = "https://test"
            };

            await settingsRepo.UpsertAsync(settings, TestContext.Current.CancellationToken);

            var settingsService = new GoogleSettingsService(settingsRepo);

            var unmasked = await settingsService.GetOptionsForAuthenticationAsync(TestContext.Current.CancellationToken);

            Assert.Equal("test-client", unmasked.ClientId);
            Assert.Equal("super-secret", unmasked.ClientSecret);
            Assert.Equal("test-project", unmasked.ProjectId);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private class DummyHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new FakeHandler());
        private class FakeHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest));
        }
    }
}