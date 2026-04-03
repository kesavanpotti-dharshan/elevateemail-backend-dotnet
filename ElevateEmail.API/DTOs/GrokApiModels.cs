using System.Text.Json.Serialization;

namespace ElevateEmail.API.DTOs;

// ═══════════════════════════════════════════════════════════════════════════════
// OUTBOUND — models serialised and sent to the Grok API
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Maps to the Grok /v1/chat/completions request body.
/// Property names use JsonPropertyName to match the API's snake_case contract exactly.
/// </summary>
public sealed class GrokChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; init; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<GrokMessage> Messages { get; init; } = [];

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; init; }

    [JsonPropertyName("temperature")]
    public double Temperature { get; init; } = 0.7;
}

public sealed class GrokMessage
{
    [JsonPropertyName("role")]
    public string Role { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════════════════
// INBOUND — models deserialised from the Grok API response
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Maps to the Grok /v1/chat/completions response body.
/// </summary>
public sealed class GrokChatResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("choices")]
    public List<GrokChoice> Choices { get; init; } = [];

    [JsonPropertyName("usage")]
    public GrokUsage? Usage { get; init; }
}

public sealed class GrokChoice
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("message")]
    public GrokMessage? Message { get; init; }

    [JsonPropertyName("finish_reason")]
    public string FinishReason { get; init; } = string.Empty;
}

/// <summary>
/// Token usage reported by the Grok API.
/// Already modelled here so Phase 3 (usage tracking) can persist this
/// to the database with a single line addition in EmailRewriteService.
/// </summary>
public sealed class GrokUsage
{
    [JsonPropertyName("prompt_tokens")]
    public int PromptTokens { get; init; }

    [JsonPropertyName("completion_tokens")]
    public int CompletionTokens { get; init; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; init; }
}