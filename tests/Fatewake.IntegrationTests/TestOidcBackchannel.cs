using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace Fatewake.IntegrationTests;

/// <summary>Emulates an OIDC token endpoint, enforcing authorization-code PKCE and signing nonce-bound test ID tokens.</summary>
/// <see href="../../docs/code/tests/Fatewake.IntegrationTests/TestOidcBackchannel.md">Documentation</see>
public sealed class TestOidcBackchannel(SecurityKey key) : HttpMessageHandler
{
    /// <summary>Nonce from the browser challenge.</summary>
    public string Nonce { get; set; } = "";
    /// <summary>PKCE challenge from the browser challenge.</summary>
    public string CodeChallenge { get; set; } = "";
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath != "/token") throw new InvalidOperationException("Unexpected OIDC backchannel request.");
        var form = await request.Content!.ReadAsStringAsync(cancellationToken);
        var values = form.Split('&').Select(x => x.Split('=', 2)).ToDictionary(x => x[0], x => Uri.UnescapeDataString(x[1]));
        if (values["code"] != "test-code" || values["grant_type"] != "authorization_code" ||
            Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(values["code_verifier"]))) != CodeChallenge)
            throw new InvalidOperationException("Authorization code or PKCE mismatch.");
        var now = DateTime.UtcNow;
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("https://provider.test", "test-client",
            [new Claim("sub", "oidc-player"), new Claim("email", "oidc@example.com"), new Claim("email_verified", "true"), new Claim("nonce", Nonce),
             new Claim("iat", new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)],
            now.AddMinutes(-1), now.AddMinutes(5), new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
        return new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(new { id_token = token, access_token = "opaque-provider-token",
                token_type = "Bearer", expires_in = 300 }), Encoding.UTF8, "application/json")
        };
    }
}
