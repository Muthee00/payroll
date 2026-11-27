using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using PayMaster.Application.Common.Email;

namespace PayMaster.Infrastructure.Services;

public sealed class EmailOptions
{
    public const string Section = "Email";

    /// <summary>"Smtp" sends through the configured server; "Pickup" writes .eml files to <see cref="PickupDirectory"/> (development).</summary>
    public string Provider { get; set; } = "Pickup";
    public string FromAddress { get; set; } = "no-reply@paymaster.local";
    public string FromName { get; set; } = "PayMaster";
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string PickupDirectory { get; set; } = "mail-pickup";
}

public sealed class EmailSender(IOptions<EmailOptions> options, ILogger<EmailSender> logger) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = Build(message);

        if (string.Equals(_options.Provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(_options.Host))
                throw new InvalidOperationException("Email:Host must be configured when Email:Provider is Smtp.");

            using var client = new SmtpClient();
            await client.ConnectAsync(_options.Host, _options.Port,
                _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
            if (!string.IsNullOrEmpty(_options.UserName))
                await client.AuthenticateAsync(_options.UserName, _options.Password ?? string.Empty, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
        }
        else
        {
            Directory.CreateDirectory(_options.PickupDirectory);
            var path = Path.Combine(_options.PickupDirectory, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml");
            await mime.WriteToAsync(path, ct);
        }

        logger.LogInformation("Email '{Subject}' sent to {Recipient} via {Provider}", message.Subject, message.To, _options.Provider);
    }

    private MimeMessage Build(EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.To, message.To));
        mime.Subject = message.Subject;

        var body = new BodyBuilder { HtmlBody = message.HtmlBody };
        foreach (var attachment in message.Attachments ?? [])
            body.Attachments.Add(attachment.FileName, attachment.Content, ContentType.Parse(attachment.ContentType));
        mime.Body = body.ToMessageBody();
        return mime;
    }
}
