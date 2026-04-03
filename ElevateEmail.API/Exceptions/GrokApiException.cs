namespace ElevateEmail.API.Exceptions;

/// <summary>
/// Thrown when the Grok API call fails for any reason:
///   - HTTP error status codes (4xx, 5xx from Grok)
///   - Transport failures (DNS, connection refused)
///   - Timeout (HttpClient's internal timeout fired)
///   - Unparseable or empty response body
///
/// The GlobalExceptionMiddleware catches this and maps it to the correct
/// HTTP status code without leaking internal details to the client.
/// </summary>
public sealed class GrokApiException : Exception
{
    /// <summary>
    /// The HTTP status code returned by the Grok API, when available.
    /// Null for transport-level failures (timeout, DNS failure, etc.)
    /// where no HTTP response was received at all.
    /// </summary>
    public int? StatusCode { get; }

    public GrokApiException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public GrokApiException(string message, Exception innerException, int? statusCode = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}