using ElevateEmail.API.DTOs;
using ElevateEmail.API.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ElevateEmail.API.Controllers;

/// <summary>
/// Handles all email rewriting HTTP operations.
///
/// This controller is deliberately thin — it handles HTTP concerns only:
///   - Routing
///   - Request/response translation
///   - Logging the inbound request
///   - Returning the correct HTTP status codes
///
/// All business logic lives in IEmailRewriteService.
/// All AI communication lives in GrokApiClient.
/// </summary>
[ApiController]
[Route("api/email")]
[Produces("application/json")]
public sealed class EmailController : ControllerBase
{
    private readonly IEmailRewriteService _emailRewriteService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailRewriteService     emailRewriteService,
        ILogger<EmailController> logger)
    {
        _emailRewriteService = emailRewriteService;
        _logger              = logger;
    }

    /// <summary>
    /// Rewrites an email using AI based on the specified tone and length preferences.
    /// </summary>
    /// <param name="request">Email content and rewriting preferences.</param>
    /// <param name="cancellationToken">
    /// Automatically cancelled when the HTTP connection is closed (e.g. user navigates away).
    /// Propagated all the way to the Grok HTTP call to avoid paying for unused API responses.
    /// </param>
    /// <returns>The AI-rewritten email with applied settings and improvement notes.</returns>
    /// <response code="200">Email successfully rewritten.</response>
    /// <response code="400">Validation failed — emailContent is missing, too short, or too long.</response>
    /// <response code="502">Grok API returned an error response.</response>
    /// <response code="504">Grok API call timed out.</response>
    [HttpPost("rewrite")]
    [ProducesResponseType(typeof(EmailRewriteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status504GatewayTimeout)]
    public async Task<IActionResult> RewriteEmail(
        [FromBody] EmailRewriteRequest request,
        CancellationToken              cancellationToken)
    {
        // [ApiController] automatically returns HTTP 400 with a ValidationProblemDetails
        // body if DataAnnotations on EmailRewriteRequest fail. We never reach this line
        // with an invalid request — the framework handles it before calling the action.

        _logger.LogInformation(
            "POST /api/email/rewrite — IP={IP} ContentLength={Chars} Tone={Tone} Length={Length}",
            HttpContext.Connection.RemoteIpAddress,
            request.EmailContent.Length,
            request.Tone,
            request.Length);

        var result = await _emailRewriteService.RewriteEmailAsync(request, cancellationToken);

        return Ok(result);
    }
}