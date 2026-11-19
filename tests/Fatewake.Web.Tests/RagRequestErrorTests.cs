using System.Net;
using System.Net.Http.Json;
using Fatewake.Web.Presentation;

namespace Fatewake.Web.Tests;

/// <summary>Checks actionable, bounded RAG error details without exposing arbitrary response bodies.</summary>
/// <see href="../../docs/code/tests/Fatewake.Web.Tests/RagRequestErrorTests.md">Documentation</see>
public sealed class RagRequestErrorTests
{
    /// <summary>Derives lowercase dash-separated keys from titles and drops unsupported characters.</summary>
    [Theory]
    [InlineData("The Wayfinder Legacy — Volume One", "the-wayfinder-legacy-volume-one")]
    [InlineData("  Spaced   Out  Title ", "spaced-out-title")]
    [InlineData("Day 1: Eli's Story!", "day-1-elis-story")]
    public void Slug_follows_title(string title, string expected) =>
        Assert.Equal(expected, RagKeySlug.FromTitle(title, 200));

    /// <summary>Respects the field length without leaving a trailing dash.</summary>
    [Fact]
    public void Slug_is_truncated_without_trailing_dash() =>
        Assert.Equal("abc", RagKeySlug.FromTitle("abc def", 4));

    /// <summary>Explains authorization and request-size failures even when middleware returns no body.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "rag.sign_in_required", true)]
    [InlineData(HttpStatusCode.Forbidden, "rag.forbidden", true)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "rag.upload_too_large", false)]
    public async Task Empty_errors_have_actionable_status_details(HttpStatusCode status, string code, bool authentication)
    {
        using var response = new HttpResponseMessage(status);
        response.Headers.Add("X-Request-ID", "test-reference");
        var error = await RagRequestError.ReadAsync(response, "Upload document", TestContext.Current.CancellationToken);
        Assert.Equal(code, error.Code);
        Assert.Equal((int)status, error.StatusCode);
        Assert.Equal(authentication, error.RequiresAuthentication);
        Assert.Equal("test-reference", error.ReferenceId);
        Assert.False(string.IsNullOrWhiteSpace(error.Detail));
    }

    /// <summary>Preserves the safe explanation and correlation from API Problem Details.</summary>
    [Fact]
    public async Task Problem_details_are_available_for_the_popup()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { detail = "Source key and title are required.", code = "rag.metadata", traceId = "trace-reference" })
        };
        var error = await RagRequestError.ReadAsync(response, "Upload document", TestContext.Current.CancellationToken);
        Assert.Equal("Source key and title are required.", error.Detail);
        Assert.Equal("rag.metadata", error.Code);
        Assert.Equal("trace-reference", error.ReferenceId);
    }

    /// <summary>Does not turn HTML exception pages or provider bodies into administrator-facing diagnostics.</summary>
    [Fact]
    public async Task Raw_server_error_pages_are_never_displayed()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("<html>private-diagnostic-marker</html>", System.Text.Encoding.UTF8, "text/html")
        };
        var error = await RagRequestError.ReadAsync(response, "Upload document", TestContext.Current.CancellationToken);
        Assert.DoesNotContain("private-diagnostic-marker", error.ToString());
    }

    /// <summary>Reports malformed or oversized diagnostic bodies without silently treating them as successful responses.</summary>
    [Theory]
    [InlineData(false, "malformed JSON")]
    [InlineData(true, "safe display limit")]
    public async Task Invalid_diagnostics_are_bounded_and_explained(bool oversized, string explanation)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(oversized ? new string('x', 17000) : "{", System.Text.Encoding.UTF8, "application/problem+json")
        };
        var error = await RagRequestError.ReadAsync(response, "Upload document", TestContext.Current.CancellationToken);
        Assert.Contains(explanation, error.Detail);
        Assert.True(error.Detail.Length < 1000);
    }
}
