using ElevateEmail.API.DTOs;

namespace ElevateEmail.API.Services.Interfaces;

/// <summary>
/// Contract for the email rewriting service.
///
/// Why an interface here?
///   - EmailController depends on this abstraction, not the concrete class.
///     Swapping implementations (mock, cached, multi-provider) requires zero controller changes.
///   - Unit tests inject a mock/stub of this interface without spinning up HttpClient or hitting Grok.
///   - DI registration in Program.cs is one line: AddScoped&lt;IEmailRewriteService, EmailRewriteService&gt;()
/// </summary>
public interface IEmailRewriteService
{
    /// <summary>
    /// Rewrites the supplied email using the Grok AI API.
    /// </summary>
    /// <param name="request">
    /// Validated inbound request — emailContent, tone, and length preferences.
    /// </param>
    /// <param name="cancellationToken">
    /// Propagated from the HTTP request lifecycle.
    /// If the client disconnects mid-request, this cancels the in-flight Grok HTTP call,
    /// saving the API cost of a response nobody will receive.
    /// </param>
    /// <returns>Rewritten email, applied settings, and improvement notes.</returns>
    /// <exception cref="Exceptions.GrokApiException">
    /// Thrown if the Grok API call fails. Caught by GlobalExceptionMiddleware.
    /// </exception>
    Task<EmailRewriteResponse> RewriteEmailAsync(
        EmailRewriteRequest request,
        CancellationToken   cancellationToken = default);
}