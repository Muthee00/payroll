namespace PayMaster.Application.Common.Email;

public record EmailAttachment(string FileName, string ContentType, byte[] Content);

public record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string? ToName = null,
    IReadOnlyList<EmailAttachment>? Attachments = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
