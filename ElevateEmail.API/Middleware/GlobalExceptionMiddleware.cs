using System.Net;
using System.Text.Json;
using ElevateEmail.API.Exceptions;

namespace ElevateEmail.API.Middleware;

/// <summary>
/// Global exception handling middleware — registered first in Program.cs
/// so it wraps the entire pipeline and catches every unhandled exception.
///
/// Responsibilities:
///   - Log the full exception server-side (with TraceId for correlation)
///   - Return a clean, consistent JSON error envelope to the client
///   - Never leak stack traces, internal class names, or sensitive details
///   - Map typed exceptions to precise HTTP status codes
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception. Method={Method} Path={Path} TraceId={TraceId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await WriteErrorResponseAsync(context, ex);
        }
    }

    private static async Task WriteErrorResponseAsync(HttpContext context, Exception exception)
    {
        // Guard: don't overwrite a response that has already started streaming bytes to the client
        if (context.Response.HasStarted) return;

        context.Response.ContentType = "application/json";

        var (statusCode, userMessage) = exception switch
        {
            // Our own typed exception — use its message and optional status code
            GrokApiException grokEx => (
                grokEx.StatusCode ?? (int)HttpStatusCode.BadGateway,
                grokEx.Message
            ),

            // HttpClient timeout or request cancellation
            TaskCanceledException or OperationCanceledException => (
                (int)HttpStatusCode.GatewayTimeout,
                "The AI service did not respond in time. Please try again."
            ),

            // Anything else — never expose internal details
            _ => (
                (int)HttpStatusCode.InternalServerError,
                "An unexpected error occurred. Please try again later."
            )
        };

        context.Response.StatusCode = statusCode;

        var payload = JsonSerializer.Serialize(new ErrorEnvelope(
            Error:     userMessage,
            StatusCode: statusCode,
            TraceId:   context.TraceIdentifier,   // correlates with server log entry
            Timestamp: DateTime.UtcNow
        ), JsonOptions);

        await context.Response.WriteAsync(payload);
    }

    /// <summary>
    /// Consistent error shape returned for every failure.
    /// TraceId lets support teams grep server logs to find the full exception.
    /// </summary>
    private sealed record ErrorEnvelope(
        string   Error,
        int      StatusCode,
        string   TraceId,
        DateTime Timestamp
    );
}