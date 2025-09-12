namespace NestProxy.Server.Models;

public class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
}
