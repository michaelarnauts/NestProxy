namespace NestProxy.Server.Configuration;

/// <summary>
/// Options describing the root directory for JSON-based configuration persistence.
/// </summary>
public class ConfigStorageOptions
{
    /// <summary>
    /// Root directory where configuration files are stored.
    /// </summary>
    public string RootPath { get; set; } = "config";
}
