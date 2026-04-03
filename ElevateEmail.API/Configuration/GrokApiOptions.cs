using System.ComponentModel.DataAnnotations;

namespace ElevateEmail.API.Configuration;


/// <summary>
/// Strongly-typed configuration for the Grok (xAI) API.
/// Bound from the "GrokApi" section in appsettings.json.
///
/// IMPORTANT: ApiKey is intentionally excluded from appsettings.json.
/// Inject it via environment variable only:
///   export GrokApi__ApiKey="xai-your-key-here"        (macOS/Linux)
///   $env:GrokApi__ApiKey = "xai-your-key-here"        (Windows PowerShell)
///   dotnet user-secrets set "GrokApi:ApiKey" "..."    (local dev)
/// </summary>
public sealed class GrokApiOptions
{
    public const string SectionName = "GrokApi";

    [Required(ErrorMessage = "GrokApi:BaseUrl is required.")]
    [Url(ErrorMessage = "GrokApi:BaseUrl must be a valid URL.")]
    public string BaseUrl { get; init; } = "https://api.x.ai";

    /// <summary>
    /// Never set this in appsettings.json — inject via environment variable GrokApi__ApiKey.
    /// ValidateOnStart() in Program.cs will throw at boot if this is missing.
    /// </summary>
    [Required(ErrorMessage = "GrokApi:ApiKey is not configured. Set the GrokApi__ApiKey environment variable.")]
    public string ApiKey { get; init; } = string.Empty;

    [Required(ErrorMessage = "GrokApi:Model is required.")]
    public string Model { get; init; } = "grok-3-latest";

    [Range(5, 120, ErrorMessage = "GrokApi:TimeoutSeconds must be between 5 and 120.")]
    public int TimeoutSeconds { get; init; } = 30;

    [Range(64, 4096, ErrorMessage = "GrokApi:MaxTokens must be between 64 and 4096.")]
    public int MaxTokens { get; init; } = 1024;
}
