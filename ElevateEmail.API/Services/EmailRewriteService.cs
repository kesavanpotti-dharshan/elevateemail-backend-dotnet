using System.Text.Json;
using ElevateEmail.API.Configuration;
using ElevateEmail.API.DTOs;
using ElevateEmail.API.Exceptions;
using ElevateEmail.API.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace ElevateEmail.API.Services;

/// <summary>
/// Core application service for AI-powered email rewriting.
///
/// Responsibilities (and only these):
///   1. Normalise and validate tone/length inputs against allowed sets
///   2. Build the system and user prompts (prompt engineering lives here)
///   3. Delegate HTTP transport to GrokApiClient
///   4. Parse and validate the structured JSON response from Grok
///   5. Return a fully-populated EmailRewriteResponse DTO
///
/// GrokApiClient owns transport. This service owns logic.
/// </summary>
public sealed class EmailRewriteService : IEmailRewriteService
{
    private readonly GrokApiClient _grokClient;
    private readonly GrokApiOptions _options;
    private readonly ILogger<EmailRewriteService> _logger;

    // ── Allowed value sets ────────────────────────────────────────────────────
    // These are the single source of truth. A future GET /api/email/options
    // endpoint will expose these to the frontend so the UI dropdown stays in sync.

    private static readonly IReadOnlySet<string> AllowedTones =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Professional", "Friendly", "Formal",   "Concise",
            "Persuasive",   "Empathetic","Direct",   "Diplomatic"
        };

    private static readonly IReadOnlySet<string> AllowedLengths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Brief", "Standard", "Detailed"
        };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmailRewriteService(
        GrokApiClient              grokClient,
        IOptions<GrokApiOptions>   options,
        ILogger<EmailRewriteService> logger)
    {
        _grokClient = grokClient;
        _options    = options.Value;
        _logger     = logger;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PUBLIC
    // ═════════════════════════════════════════════════════════════════════════

    public async Task<EmailRewriteResponse> RewriteEmailAsync(
        EmailRewriteRequest request,
        CancellationToken   cancellationToken = default)
    {
        // Normalise inputs — unrecognised values silently fall back to defaults.
        // The client always knows what was applied via ToneApplied / LengthApplied.
        var tone   = Normalise(request.Tone,   AllowedTones,   "Professional");
        var length = Normalise(request.Length, AllowedLengths, "Standard");

        _logger.LogInformation(
            "Starting email rewrite. Tone={Tone} Length={Length} ContentChars={Chars}",
            tone, length, request.EmailContent.Length);

        var grokRequest = new GrokChatRequest
        {
            Model       = _options.Model,
            MaxTokens   = _options.MaxTokens,
            Temperature = 0.7,
            Messages    =
            [
                new GrokMessage { Role = "system", Content = BuildSystemPrompt()                               },
                new GrokMessage { Role = "user",   Content = BuildUserPrompt(request.EmailContent, tone, length) }
            ]
        };

        var rawResponse = await _grokClient.CompleteChatAsync(grokRequest, cancellationToken);

        var result = ParseStructuredResponse(rawResponse, tone, length);

        _logger.LogInformation(
            "Email rewrite completed successfully. Tone={Tone} Length={Length}",
            result.ToneApplied, result.LengthApplied);

        return result;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PROMPT ENGINEERING
    // ═════════════════════════════════════════════════════════════════════════

    private static string BuildSystemPrompt() =>
        """
        You are ElevateEmail, an expert business writing assistant specialising in
        transforming emails to be clearer, more effective, and professionally compelling.

        When given an email to rewrite, you MUST respond with valid JSON only.
        Do NOT include markdown code fences, preamble, commentary, or any text outside the JSON object.

        Your JSON response must follow this exact schema:
        {
          "rewrittenEmail": "the full rewritten email as a single string, using \n for line breaks",
          "improvementNotes": "2-3 concise bullet points prefixed with • explaining key improvements made"
        }

        Always ensure "rewrittenEmail" is a properly structured email with:
        - An appropriate greeting
        - A clear, well-organised body
        - A professional closing and signature placeholder
        """;

    private static string BuildUserPrompt(string emailContent, string tone, string length)
    {
        var lengthInstruction = length switch
        {
            "Brief"    => "Be concise — ruthlessly remove every non-essential sentence and filler phrase.",
            "Detailed" => "Be thorough — expand context, add relevant detail, and ensure nothing important is omitted.",
            _          => "Maintain a similar length to the original, adjusting only where clarity genuinely improves."
        };

        return $"""
            Please rewrite the following email.

            Required tone:   {tone}
            Length guidance: {lengthInstruction}

            Original email:
            ───────────────────────────────────────────────────────
            {emailContent}
            ───────────────────────────────────────────────────────

            Respond with the JSON schema defined in your system instructions only. No other text.
            """;
    }

    // ═════════════════════════════════════════════════════════════════════════
    // RESPONSE PARSING
    // ═════════════════════════════════════════════════════════════════════════

    private EmailRewriteResponse ParseStructuredResponse(string rawJson, string tone, string length)
    {
        // Defensive strip — despite the system prompt, models occasionally wrap
        // JSON in a ```json ... ``` fence. This handles it gracefully.
        var cleanJson = StripMarkdownFence(rawJson);

        try
        {
            using var doc  = JsonDocument.Parse(cleanJson);
            var root       = doc.RootElement;

            var rewrittenEmail = root.TryGetProperty("rewrittenEmail", out var re)
                ? re.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            var improvementNotes = root.TryGetProperty("improvementNotes", out var notes)
                ? notes.GetString()?.Trim() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(rewrittenEmail))
            {
                _logger.LogError(
                    "Parsed Grok response had empty rewrittenEmail field. Raw={Raw}", rawJson);

                throw new GrokApiException(
                    "The AI returned a valid response structure but the rewritten email content was empty.");
            }

            return new EmailRewriteResponse
            {
                RewrittenEmail   = rewrittenEmail,
                ToneApplied      = tone,
                LengthApplied    = length,
                ImprovementNotes = improvementNotes
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "JSON parse failure on Grok response. Raw={Raw}", rawJson);

            throw new GrokApiException(
                "The AI service returned a response that could not be parsed. Please try again.", ex);
        }
    }

    // ═════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Returns the canonical casing from the allowed set when a match is found,
    /// otherwise returns the default. Matching is case-insensitive.
    /// Example: "PROFESSIONAL" → "Professional", "unknown" → "Professional"
    /// </summary>
    private static string Normalise(
        string?             value,
        IReadOnlySet<string> allowed,
        string              defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;

        // HashSet<string> with OrdinalIgnoreCase comparer: TryGetValue returns
        // the actual stored string (correct casing) when found.
        return allowed.TryGetValue(value.Trim(), out var canonical)
            ? canonical
            : defaultValue;
    }

    /// <summary>
    /// Strips ```json ... ``` or ``` ... ``` fences that models occasionally
    /// add despite instructions. Does nothing if no fence is detected.
    /// </summary>
    private static string StripMarkdownFence(string raw)
    {
        var trimmed = raw.Trim();
        if (!trimmed.StartsWith("```")) return trimmed;

        var firstNewline = trimmed.IndexOf('\n');
        var lastFence    = trimmed.LastIndexOf("```");

        if (firstNewline > 0 && lastFence > firstNewline)
            return trimmed[(firstNewline + 1)..lastFence].Trim();

        return trimmed;
    }
}