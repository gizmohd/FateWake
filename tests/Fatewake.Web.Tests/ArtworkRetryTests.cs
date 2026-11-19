using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Channels;
using Fatewake.Web.Authentication;
using Fatewake.Web.Components.Presentation;
using Fatewake.Web.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fatewake.Web.Tests;

/// <summary>Exercises the interactive scene artwork placeholder and retrieval lifecycle.</summary>
/// <see href="../../../docs/code/tests/Fatewake.Web.Tests/ArtworkRetryTests.md">ArtworkRetryTests documentation</see>
public sealed class ArtworkRetryTests
{
    /// <summary>Pending work and failed image downloads both retry after five seconds, without a page refresh.</summary>
    [Fact]
    public async Task Pending_art_and_failed_image_downloads_retry_automatically()
    {
        var ct = TestContext.Current.CancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(25));
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<AuthenticationStateProvider, Guest>();
        services.AddSingleton<IHttpClientFactory, ArtResponses>();
        services.AddScoped<AuthenticatedApiClient>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new InteractiveRenderer(provider);
        var updates = Channel.CreateUnbounded<long>();
        var id = await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ArtCompositionView.ArtworkKey)] = "day1-phone-0617",
                [nameof(ArtCompositionView.Layers)] = DayOneArt.For("day1-phone-0617").Take(1).ToArray(),
                [nameof(ArtCompositionView.ArtworkDisplayed)] = EventCallback.Factory.Create<ArtworkSnapshot>(
                    this, _ => { Assert.True(updates.Writer.TryWrite(Stopwatch.GetTimestamp())); })
            })));

        var pending = await updates.Reader.ReadAsync(timeout.Token);
        Assert.Equal(("/images/art-generating.svg", 0UL), await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id)));

        var completed = await updates.Reader.ReadAsync(timeout.Token);
        var ready = await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id));
        Assert.True(Stopwatch.GetElapsedTime(pending, completed) >= TimeSpan.FromSeconds(4.9));
        Assert.Equal("/art/bg-day1-bedroom-dark/1/png", ready.Url);
        Assert.NotEqual(0UL, ready.ErrorHandler);

        var failed = Stopwatch.GetTimestamp();
        await renderer.Dispatcher.InvokeAsync(() => renderer.ReportErrorAsync(ready.ErrorHandler));
        var fallback = await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id));
        Assert.Equal("/images/art-generating.svg", fallback.Url);
        Assert.Equal(0UL, fallback.ErrorHandler);

        var recovered = await updates.Reader.ReadAsync(timeout.Token);
        var retried = await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id));
        Assert.True(Stopwatch.GetElapsedTime(failed, recovered) >= TimeSpan.FromSeconds(4.9));
        Assert.Equal(ready.Url, retried.Url);
        Assert.NotEqual(0UL, retried.ErrorHandler);
        Assert.NotEqual(ready.ErrorHandler, retried.ErrorHandler);
    }

    private sealed class Guest : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new(new System.Security.Claims.ClaimsIdentity())));
    }

    private sealed class ArtResponses : HttpMessageHandler, IHttpClientFactory
    {
        private int requests;

        public HttpClient CreateClient(string name) =>
            new(this, disposeHandler: false) { BaseAddress = new Uri("http://test") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.AbsolutePath != "/api/art/scene/resolve")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));

            var ready = Interlocked.Increment(ref requests) > 1;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[] { new { key = "bg-day1-bedroom-dark", status = ready ? "ready" : "pending",
                    version = ready ? 1 : (int?)null } })
            });
        }
    }

#pragma warning disable BL0006 // Inspect interactive render frames to exercise the browser image error callback.
    private sealed class InteractiveRenderer(IServiceProvider services)
        : Renderer(services, services.GetRequiredService<ILoggerFactory>())
    {
        public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

        public async Task<int> MountAsync(ParameterView parameters)
        {
            var id = AssignRootComponentId(InstantiateComponent(typeof(ArtCompositionView)));
            await RenderRootComponentAsync(id, parameters);
            return id;
        }

        public Task ReportErrorAsync(ulong handler) => DispatchEventAsync(handler, null, EventArgs.Empty);

        public (string? Url, ulong ErrorHandler) ImageFrame(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Markup &&
                    frame.MarkupContent.Contains("src=\"/images/art-generating.svg\"", StringComparison.Ordinal))
                    return ("/images/art-generating.svg", 0);
                if (frame is not { FrameType: RenderTreeFrameType.Element, ElementName: "img" }) continue;
                string? url = null;
                ulong handler = 0;
                for (var j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                {
                    if (frames.Array[j].AttributeName == "src") url = frames.Array[j].AttributeValue as string;
                    if (frames.Array[j].AttributeName == "onerror") handler = frames.Array[j].AttributeEventHandlerId;
                }
                return (url, handler);
            }
            return default;
        }

        protected override void HandleException(Exception exception) => throw new InvalidOperationException("Interactive render failed.", exception);

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
#pragma warning restore BL0006
}
