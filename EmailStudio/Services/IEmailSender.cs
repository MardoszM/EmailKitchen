namespace EmailStudio.Services;

public interface IEmailSender
{
    Task SendHtmlAsync(string to, string subject, string html, CancellationToken ct = default);
}
