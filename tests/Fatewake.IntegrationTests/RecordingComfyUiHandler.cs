using System.Net;
using System.Text;

namespace Fatewake.IntegrationTests;

/// <summary>Emulates the ComfyUI <c>/prompt</c>, <c>/history</c>, and redirecting <c>/view</c> endpoints while recording every request.</summary>
/// <see href="../../../docs/code/tests/Fatewake.IntegrationTests/RecordingComfyUiHandler.md">RecordingComfyUiHandler documentation</see>
public sealed class RecordingComfyUiHandler : HttpMessageHandler
{
    /// <summary>Signed storage URL returned as the <c>/view</c> redirect target.</summary>
    public static readonly Uri StorageUrl = new("https://storage.test/signed/master.png");

    /// <summary>Image bytes served from <see cref="StorageUrl"/>.</summary>
    public static readonly byte[] ImageBytes = [1, 2, 3, 4];

    /// <summary>Requests received in order, with their URI and headers captured before disposal.</summary>
    public List<(Uri Uri, Dictionary<string, string> Headers)> Requests { get; } = [];

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        Requests.Add((uri, request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value), StringComparer.OrdinalIgnoreCase)));
        var path = uri.AbsolutePath;
        if (uri.Host == StorageUrl.Host)
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(ImageBytes) });
        if (path.EndsWith("/prompt", StringComparison.Ordinal))
            return Json("""{"prompt_id":"p1"}""");
        if (path.EndsWith("/history/p1", StringComparison.Ordinal))
            return Json("""{"p1":{"outputs":{"9":{"images":[{"filename":"m.png","subfolder":"","type":"output"}]}}}}""");
        if (path.EndsWith("/view", StringComparison.Ordinal))
        {
            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = StorageUrl;
            return Task.FromResult(redirect);
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private static Task<HttpResponseMessage> Json(string body) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
}
