namespace Fatewake.Infrastructure.Authentication;

/// <summary>SMTP transport and explicit development text-file delivery configuration.</summary>
/// <see href="../../../docs/code/src/Fatewake.Infrastructure/Authentication/EmailDeliveryOptions.md">Documentation</see>
public sealed class EmailDeliveryOptions
{
    /// <summary>Enables SMTP; otherwise messages are written to text files.</summary>
    public bool SmtpEnabled { get; set; }
    /// <summary>SMTP server hostname.</summary>
    public string Host { get; set; } = "";
    /// <summary>SMTP STARTTLS port.</summary>
    public int Port { get; set; } = 587;
    /// <summary>Requires STARTTLS transport encryption.</summary>
    public bool EnableSsl { get; set; } = true;
    /// <summary>Optional SMTP username.</summary>
    public string? Username { get; set; }
    /// <summary>SMTP secret supplied from protected configuration.</summary>
    public string? Password { get; set; }
    /// <summary>Sender accepted by the server.</summary>
    public string FromAddress { get; set; } = "noreply@fatewake.local";
    /// <summary>Trusted public origin for verification links.</summary>
    public string PublicWebUrl { get; set; } = "https://localhost:63279";
    /// <summary>Private fallback text-email directory.</summary>
    public string Directory { get; set; } = Path.Combine(AppContext.BaseDirectory, "emails");
}
