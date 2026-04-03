using System.ComponentModel.DataAnnotations;

namespace ElevateEmail.API.DTOs;

/// <summary>
/// Inbound request body from the React frontend.
/// DataAnnotations are enforced automatically by [ApiController] before
/// the request reaches the controller action — no manual validation needed.
/// </summary>
public sealed class EmailRewriteRequest
{
    /// <summary>
    /// The original email body the user wants rewritten.
    /// </summary>
    /// <example>hey john just wanted to check in on that report you were supposed to send last week...</example>
    [Required(ErrorMessage = "emailContent is required and cannot be empty.")]
    [MinLength(10, ErrorMessage = "emailContent must be at least 10 characters.")]
    [MaxLength(8000, ErrorMessage = "emailContent cannot exceed 8,000 characters.")]
    public string EmailContent { get; init; } = string.Empty;

    /// <summary>
    /// Desired tone for the rewritten email.
    /// Defaults to "Professional" if omitted or if an unrecognised value is provided.
    /// Supported: Professional, Friendly, Formal, Concise, Persuasive, Empathetic, Direct, Diplomatic.
    /// </summary>
    /// <example>Professional</example>
    public string Tone { get; init; } = "Professional";

    /// <summary>
    /// Desired length adjustment for the rewritten email.
    /// Defaults to "Standard" if omitted or if an unrecognised value is provided.
    /// Supported: Brief, Standard, Detailed.
    /// </summary>
    /// <example>Standard</example>
    public string Length { get; init; } = "Standard";
}