using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Fatewake.IntegrationTests;

/// <summary>Owns an isolated verification-email directory and resolves emailed tokens without exposing them in test output.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/TestEmailMailbox.md">Documentation</see>
public sealed class TestEmailMailbox : IDisposable
{
    /// <summary>Unique mailbox owned and cleaned up by this fixture.</summary>
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "fatewake-emails-" + Guid.NewGuid().ToString("N"));
    /// <summary>File-delivery email sender.</summary>
    public VerificationEmailSender Sender => new(Options.Create(new EmailDeliveryOptions { Directory = DirectoryPath }),
        NullLogger<VerificationEmailSender>.Instance);
    /// <summary>Creates a verification service sharing the fixture mailbox.</summary>
    public VerifiedAccountService Verification(FatewakeDbContext db) => new(db, Sender, NullLogger<VerifiedAccountService>.Instance,
        new PasswordHasher<LocalCredentialRecord>());
    /// <summary>Creates a local account service sharing the fixture mailbox.</summary>
    public LocalAccountService Accounts(FatewakeDbContext db, IPasswordHasher<LocalCredentialRecord> hasher) =>
        new(db, hasher, NullLogger<LocalAccountService>.Instance, Verification(db));
    /// <summary>Reads the most recent verification token for an address.</summary>
    public string Token(string email)
    {
        var file = Directory.GetFiles(DirectoryPath, "*.txt").OrderByDescending(File.GetLastWriteTimeUtc)
            .First(x => File.ReadAllText(x).StartsWith("To: " + email + "\n", StringComparison.OrdinalIgnoreCase));
        var link = File.ReadAllLines(file).Single(x => x.StartsWith("https://", StringComparison.Ordinal));
        return new Uri(link).Query["?token=".Length..];
    }
    /// <inheritdoc />
    public void Dispose()
    {
        if (!Directory.Exists(DirectoryPath)) return;
        foreach (var file in Directory.GetFiles(DirectoryPath, "*.txt")) File.Delete(file);
        Directory.Delete(DirectoryPath);
    }
}
