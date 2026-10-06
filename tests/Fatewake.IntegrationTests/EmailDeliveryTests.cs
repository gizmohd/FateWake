using System.Net;
using System.Net.Sockets;
using System.Text;
using Fatewake.Infrastructure.Authentication;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies actual SMTP delivery, disabled-SMTP text output, and explicit transport failures.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/EmailDeliveryTests.md">Documentation</see>
public sealed class EmailDeliveryTests
{
    /// <summary>Disabled SMTP writes a uniquely named text email containing the configured public verification URL.</summary>
    [Fact]
    public async Task Disabled_smtp_writes_text_email()
    {
        using var mailbox = new TestEmailMailbox();
        var token = new string('A', 64);
        await mailbox.Sender.SendAsync("player@example.com", token, TestContext.Current.CancellationToken);
        Assert.Equal(token, mailbox.Token("player@example.com"));
        var file = Assert.Single(Directory.GetFiles(mailbox.DirectoryPath, "*.txt"));
        var contents = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        Assert.Contains("Subject: Verify your Fatewake email", contents);
        Assert.Contains("expires in 24 hours", contents);
        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
    }

    /// <summary>Enabled SMTP sends through the configured server and never falls back to disk.</summary>
    [Fact]
    public async Task Enabled_smtp_delivers_and_reports_connection_failure()
    {
        var ct = TestContext.Current.CancellationToken;
        using var mailbox = new TestEmailMailbox();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var socket = await listener.AcceptTcpClientAsync(ct);
            using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            await writer.WriteLineAsync("220 localhost SMTP ready");
            var body = new StringBuilder();
            while (await reader.ReadLineAsync(ct) is {} line)
            {
                if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 localhost");
                else if (line.StartsWith("MAIL FROM") || line.StartsWith("RCPT TO")) await writer.WriteLineAsync("250 OK");
                else if (line == "DATA")
                {
                    await writer.WriteLineAsync("354 Send data");
                    while (await reader.ReadLineAsync(ct) is {} data && data != ".") body.AppendLine(data);
                    await writer.WriteLineAsync("250 queued");
                }
                else if (line == "QUIT") { await writer.WriteLineAsync("221 bye"); break; }
                else throw new InvalidOperationException("Unexpected SMTP command.");
            }
            return body.ToString();
        }, ct);
        var sender = new VerificationEmailSender(Options.Create(new EmailDeliveryOptions
        {
            SmtpEnabled = true, Host = "127.0.0.1", Port = port, EnableSsl = false, Directory = mailbox.DirectoryPath
        }), NullLogger<VerificationEmailSender>.Instance);
        await sender.SendAsync("player@example.com", new string('B', 64), ct);
        var message = await received.WaitAsync(TimeSpan.FromSeconds(15), ct);
        Assert.Contains("To: player@example.com", message);
        Assert.Contains("Verify your Fatewake email", message);
        Assert.Contains("/verify-email?token=", message);
        Assert.False(Directory.Exists(mailbox.DirectoryPath));
        listener.Stop();
        await Assert.ThrowsAsync<System.Net.Mail.SmtpException>(() => sender.SendAsync("player@example.com", new string('C', 64), ct));
        Assert.False(Directory.Exists(mailbox.DirectoryPath));
    }
}
