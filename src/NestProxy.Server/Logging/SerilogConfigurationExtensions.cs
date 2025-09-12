using Serilog;

namespace NestProxy.Server.Logging;

/// <summary>
/// Provides extension methods for configuring Serilog as the logging provider in ASP.NET Core applications.
/// </summary>
/// <remarks>This static class contains methods that integrate Serilog with the application's logging
/// infrastructure. Use these methods to replace the default logging provider with Serilog, enabling advanced logging
/// features and configuration options.</remarks>
public static class SerilogConfigurationExtensions
{
    /// <summary>
    /// Configures Serilog as the logging provider for the specified web application builder.
    /// </summary>
    /// <remarks>This method sets up Serilog with configuration from the application's settings and registers
    /// it as the logging provider. It overrides default log levels for common Microsoft and System namespaces to reduce
    /// log verbosity. Call this method early in the application's startup to ensure all logging uses Serilog.</remarks>
    /// <param name="builder">The web application builder to configure with Serilog logging. Cannot be null.</param>
    /// <returns>The same <see cref="WebApplicationBuilder"/> instance, configured to use Serilog for logging.</returns>
    public static WebApplicationBuilder AddSerilogLogging(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", Serilog.Events.LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Extensions.Http", Serilog.Events.LogEventLevel.Warning)
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        builder.Host.UseSerilog();
        return builder;
    }
}