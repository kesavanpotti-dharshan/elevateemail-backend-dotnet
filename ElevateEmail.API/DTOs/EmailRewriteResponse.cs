namespace ElevateEmail.API.DTOs;

/// <summary>
/// Outbound response sent to the React frontend after a successful rewrite.
/// All fields are always present — no nullable members to guard against on the client.
/// </summary>
public sealed class EmailRewriteResponse
{
    /// <summary>
    /// The AI-rewritten email body, ready to copy-paste.
    /// </summary>
    public string RewrittenEmail { get; init; } = string.Empty;

    /// <summary>
    /// The tone that was actually applied.
    /// This is the normalised canonical value — useful if the client sent a non-standard casing.
    /// </summary>
    public string ToneApplied { get; init; } = string.Empty;

    /// <summary>
    /// The length setting that was actually applied.
    /// Normalised canonical value — "Brief", "Standard", or "Detailed".
    /// </summary>
    public string LengthApplied { get; init; } = string.Empty;

    /// <summary>
    /// 2–3 bullet points from the AI explaining what was improved.
    /// Helps users understand the changes and builds trust in the output.
    /// </summary>
    public string ImprovementNotes { get; init; } = string.Empty;
}