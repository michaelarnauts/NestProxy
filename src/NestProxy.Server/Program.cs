using Microsoft.Extensions.Options;

using NestProxy.Server.Configuration;
using NestProxy.Server.Data;
using NestProxy.Server.Logging;
using NestProxy.Server.Services;
using NestProxy.Server.Streaming.Downstream.Server;

using Serilog;

// Configure bootstrap logger.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting NestProxy Server...");

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.AddSerilogLogging();

    // Config options - Note: Google settings are managed via database through IGoogleSettingsService
    builder.Services.Configure<StreamingOptions>(builder.Configuration.GetSection("Streaming"));
    builder.Services.Configure<ConfigStorageOptions>(builder.Configuration.GetSection("ConfigStorage"));

    // Register options validator for StreamingOptions.
    builder.Services.AddSingleton<IValidateOptions<StreamingOptions>, StreamingOptionsValidator>();

    // Http client for Google with Polly resilience policies.
    builder.Services.AddHttpClient("google", client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddPolicyHandler((serviceProvider, request) =>
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        return ResiliencePolicies.CreateHttpPolicy(logger: logger);
    });

    // Repositories
    builder.Services.AddSingleton<FileCredentialsStore>();
    builder.Services.AddSingleton<ICameraRepository, FileCameraRepository>();
    builder.Services.AddSingleton<IGoogleSettingsRepository, GoogleSettingsRepository>();

    // Core RTSP/local streaming support singletons
    builder.Services.AddSingleton<BindingSelector>();
    builder.Services.AddSingleton<CameraRegistry>();
    builder.Services.AddSingleton<IStatisticsReporter, StatisticsReporter>();
    builder.Services.AddSingleton<ISessionRegistry, SessionRegistry>();
    builder.Services.AddSingleton<ITransportAllocator, TransportAllocator>();
    builder.Services.AddSingleton<IDescribeSynthesizer, DescribeSynthesizer>();
    builder.Services.AddSingleton<IControlMapBuilder, ControlMapBuilder>();
    builder.Services.AddSingleton<ISdpRewriter, SdpRewriter>();
    builder.Services.AddSingleton<IStreamForwarder, StreamForwarder>();
    builder.Services.AddSingleton<IRtpPacketParser, RtpPacketParser>();
    builder.Services.AddSingleton<ITransportNegotiator, TransportNegotiator>();
    builder.Services.AddSingleton<ITransportSetupService, TransportSetupService>();
    builder.Services.AddSingleton<ICameraRuntimeState, CameraRuntimeState>();

    // RTSP request dispatching components
    builder.Services.AddSingleton<IRtspResponseFormatter, RtspResponseFormatter>();
    builder.Services.AddSingleton<IControlMapResolver, ControlMapResolver>();
    builder.Services.AddSingleton<DescribeRequestHandler>();
    builder.Services.AddSingleton<SetupRequestHandler>();
    builder.Services.AddSingleton<PlayRequestHandler>();
    builder.Services.AddSingleton<TeardownRequestHandler>();
    builder.Services.AddSingleton<IRtspRequestDispatcher, RtspRequestDispatcher>();

    // Register ListenerAcceptLoop as IListenerManager and as a hosted service
    builder.Services.AddSingleton<IListenerManager, ListenerAcceptLoop>();
    builder.Services.AddHostedService(sp => (ListenerAcceptLoop)sp.GetRequiredService<IListenerManager>());

    // Services
    builder.Services.AddSingleton<ICameraIdProvider, CameraIdProvider>();
    builder.Services.AddScoped<IGoogleSettingsService, GoogleSettingsService>();
    builder.Services.AddScoped<IGoogleOAuthService, GoogleAuthService>();
    builder.Services.AddScoped<GoogleSdmService>();
    builder.Services.AddHostedService<CameraSupervisorService>();
    builder.Services.AddSingleton<ILocalRtspServer, LocalRtspServer>();
    builder.Services.AddSingleton<IUpstreamSessionStateMachineFactory, UpstreamSessionStateMachineFactory>();
    builder.Services.AddSingleton<IUpstreamSessionStatisticsFactory, UpstreamSessionStatisticsFactory>();
    builder.Services.AddSingleton<IUpstreamSessionFactory, UpstreamSessionFactory>();
    builder.Services.AddSingleton<IUpstreamOrchestratorFactory, UpstreamOrchestratorFactory>();
    builder.Services.AddSingleton<ICameraProxyRunnerFactory, CameraProxyRunnerFactory>();
    builder.Services.AddHostedService(sp => (LocalRtspServer)sp.GetRequiredService<ILocalRtspServer>());

    builder.Services.AddControllers();
    builder.Services.AddRazorPages();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // CORS (simple allow any for now)
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

    var app = builder.Build();

    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStopping.Register(() => Log.Information("Application stopping: ensuring camera runners are terminated."));

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseRouting();

    // Serve Blazor WebAssembly static assets
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.MapControllers();
    app.MapRazorPages();

    // Fallback to index.html for client-side routing
    app.MapFallbackToFile("index.html");

    app.Run();

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly: {Message}", ex.Message);
    return 1;
}
finally
{
    Log.CloseAndFlush();
}