namespace EmailStudio.Services;

/// <summary>
/// Sends HTML emails through the application's email delivery service.
/// </summary>
/// <remarks>
/// Register an implementation in the application's service collection to enable sending test emails from Studio.
/// Email Studio does not provide an email transport or register a sender by default.
/// </remarks>
public interface IEmailSender
{
    /// <summary>
    /// Sends an HTML email to a single recipient.
    /// </summary>
    /// <param name="to">The recipient's email address.</param>
    /// <param name="subject">The subject of the email.</param>
    /// <param name="html">The rendered HTML body of the email.</param>
    /// <param name="ct">The cancellation token for the send operation.</param>
    /// <returns>A task that completes when the send operation finishes.</returns>
    Task SendHtmlAsync(string to, string subject, string html, CancellationToken ct = default);
}
