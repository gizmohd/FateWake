using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Fatewake.Observability;

namespace Fatewake.Infrastructure.Authentication;

/// <summary>Delivers verification email via SMTP, or writes a private text file when SMTP is explicitly disabled.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/VerificationEmailSender.md">Documentation</see>
public sealed class VerificationEmailSender(IOptions<EmailDeliveryOptions> options, ILogger<VerificationEmailSender> log)
{
    /// <summary>Sends or writes a verification message; transport/storage failures propagate without fallback.</summary>
    public async Task SendAsync(string email, string token, CancellationToken ct)
    {
        using var operation = OperationTelemetry.Start("email.deliver", log);
        var settings = options.Value;
        var link = settings.PublicWebUrl.TrimEnd('/') + "/verify-email?token=" + Uri.EscapeDataString(token);
        const string subject = "Verify your Fatewake email";
        var body = $"Confirm your email address to finish signing in to Fatewake:\n\n{link}\n\nThis link expires in 24 hours and can be used once. If you did not request this, ignore this email.";
        if (settings.SmtpEnabled)
        {
            using var client = new SmtpClient(settings.Host, settings.Port) { EnableSsl = settings.EnableSsl };
            if (!string.IsNullOrWhiteSpace(settings.Username))
                client.Credentials = new NetworkCredential(settings.Username, settings.Password);
            using var message = new MailMessage(settings.FromAddress, email, subject, body);
            await client.SendMailAsync(message, ct);
            log.LogInformation("Verification email delivered via SMTP");
            return;
        }
        var directory = Path.GetFullPath(settings.Directory);
        System.IO.Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.txt");
        var fileOptions = new FileStreamOptions
        {
            Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
            Options = FileOptions.Asynchronous
        };
        if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        await using var stream = new FileStream(path, fileOptions);
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync($"To: {email}\nFrom: {settings.FromAddress}\nSubject: {subject}\n\n{body}".AsMemory(), ct);
        log.LogInformation("Verification email saved to the configured emails directory (SMTP disabled)");
    }
}
