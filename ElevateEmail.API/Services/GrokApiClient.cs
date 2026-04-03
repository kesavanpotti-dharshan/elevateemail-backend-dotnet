using System.Text;
using System.Text.Json;
using ElevateEmail.API.DTOs;
using ElevateEmail.API.Exceptions;

namespace ElevateEmail.API.Services;

/// <summary>
/// Typed HttpClient responsible exclusively for HTTP communication with the Grok API.
///
/// Single responsibility: transport only.
///   - Serialises the request to JSON
///   - Sends it to Grok
///   - Deserialises and validates the response shape
///   - Translates every failure mode into a GrokApiException
///
/// It has zero knowledge of email logic or prompt construction — that lives in EmailRewriteService.
/// Registered via AddHttpClient&lt;GrokApiClient&gt; in Program.cs so HttpClientFactory
/// manages the underlying SocketsHttpHandler lifetime and connection pooling automatically.
/// </summary>
public sealed class GrokApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GrokApiClient> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public GrokApiClient(HttpClient httpClient, ILogger<GrokApiClient> logger)
    {
        _httpClient = httpClient;
        _logger     = logger;
    }

    /// <summary>
    /// Sends a chat completion request to Grok and returns the text content of the first choice.
    /// Callers receive either a non-empty string or a GrokApiException — nothing in between.
    /// </summary>
    /// <exception cref="GrokApiException">
    /// Wraps every possible failure: HTTP errors, transport exceptions, timeouts, empty/malformed responses.
    /// </exception>
    public async Task<string> CompleteChatAsync(
        GrokChatRequest   request,
        CancellationToken cancellationToken = default)
    {
        var json    = JsonSerializer.Serialize(request, SerializerOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug(
            "Sending chat completion to Grok. Model={Model} MessageCount={Count}",
            request.Model, request.Messages.Count);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.PostAsync("/v1/chat/completions", content, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            // HttpClient's internal Timeout property fired — not a user/request cancellation.
            // Distinguish this explicitly so the middleware maps it to 504, not a generic 500.
            _logger.LogWarning(
                "Grok API request timed out after {Timeout}s.",
                _httpClient.Timeout.TotalSeconds);

            throw new GrokApiException(
                $"The AI service did not respond within {_httpClient.Timeout.TotalSeconds} seconds. Please try again.",
                ex);
        }
        catch (TaskCanceledException)
        {
            // Legitimate cancellation (client disconnected, request aborted).
            // Re-throw as-is — middleware handles it.
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP transport error communicating with Grok API.");
            throw new GrokApiException(
                "Unable to reach the AI service. Please check your connection and try again.", ex);
        }

        // ── Non-success HTTP status ───────────────────────────────────────────

        if (!response.IsSuccessStatusCode)
        {
            // Read the error body for logging — use CancellationToken.None because the
            // original token may already be cancelled at this point.
            var errorBody = await response.Content.ReadAsStringAsync(CancellationToken.None);

            _logger.LogError(
                "Grok API returned non-success status. StatusCode={StatusCode} Body={Body}",
                (int)response.StatusCode, errorBody);

            throw new GrokApiException(
                $"The AI service returned an error (HTTP {(int)response.StatusCode}). Please try again.",
                (int)response.StatusCode);
        }

        // ── Deserialise response ──────────────────────────────────────────────

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        GrokChatResponse? parsed;

        try
        {
            parsed = JsonSerializer.Deserialize<GrokChatResponse>(responseJson, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to deserialise Grok API response. Raw={Raw}", responseJson);

            throw new GrokApiException(
                "The AI service returned a response in an unexpected format. Please try again.", ex);
        }

        // ── Extract content ───────────────────────────────────────────────────

        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogError(
                "Grok API returned empty content. FinishReason={Reason} RawResponse={Raw}",
                parsed?.Choices?.FirstOrDefault()?.FinishReason ?? "unknown",
                responseJson);

            throw new GrokApiException("The AI service returned an empty response. Please try again.");
        }

        _logger.LogDebug(
            "Grok API call succeeded. FinishReason={Reason} TotalTokens={Tokens}",
            parsed!.Choices[0].FinishReason,
            parsed.Usage?.TotalTokens ?? 0);

        return text;
    }
}