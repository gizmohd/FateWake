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
    /// <summary>Prefetched art renders even when the next scene's fresh request is still waiting.</summary>
    [Fact]
    public async Task Next_scene_uses_prefetched_version_until_current_resolution_returns()
    {
        var ct = TestContext.Current.CancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        const string nextKey = "bg-day1-house-hallway";
        var service = new PrefetchResponses(nextKey);
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<AuthenticationStateProvider, Guest>();
        services.AddSingleton<IHttpClientFactory>(service);
        services.AddScoped<AuthenticatedApiClient>();
        services.AddScoped<SceneArtworkCache>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new InteractiveRenderer(provider);
        var updates = Channel.CreateUnbounded<ArtworkSnapshot>();
        var episodeId = Guid.NewGuid();

        ParameterView Scene(string key, Guid episode, params string[] upcoming) =>
            ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ArtCompositionView.ArtworkKey)] = key,
                [nameof(ArtCompositionView.Layers)] = DayOneArt.For(key),
                [nameof(ArtCompositionView.EventInstanceId)] = episode,
                [nameof(ArtCompositionView.PrefetchKeys)] = upcoming,
                [nameof(ArtCompositionView.ArtworkDisplayed)] = EventCallback.Factory.Create<ArtworkSnapshot>(
                    this, snapshot => updates.Writer.TryWrite(snapshot))
            });

        var id = await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync(Scene("day1-phone-0617", episodeId, nextKey)));
        await updates.Reader.ReadAsync(timeout.Token);
        Assert.True(await renderer.Dispatcher.InvokeAsync(() => renderer.HasImage(id, $"/art/{nextKey}/1/png")));
        await renderer.Dispatcher.InvokeAsync(() => renderer.UpdateAsync(id, Scene("day1-house-flicker", episodeId)));
        await service.Revalidating.Task.WaitAsync(timeout.Token);
        Assert.Equal($"/art/{nextKey}/1/png", (await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id))).Url);
        Assert.False(updates.Reader.TryRead(out _));

        service.Replacement.TrySetResult(2);
        var update = await updates.Reader.ReadAsync(timeout.Token);
        Assert.Equal(2, update.Versions[nextKey]);
        Assert.True(await renderer.Dispatcher.InvokeAsync(() => renderer.HasImage(id, $"/art/{nextKey}/2/png")));
        await renderer.Dispatcher.InvokeAsync(() => renderer.UpdateAsync(id, Scene("day1-house-flicker", Guid.NewGuid())));
        Assert.Equal("/images/art-generating.svg", (await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id))).Url);
    }

    /// <summary>Pending work and failed image downloads both retry after five seconds, without a page refresh.</summary>
    [Fact]
    public async Task Pending_art_and_failed_image_downloads_retry_automatically()
    {
        var ct = TestContext.Current.CancellationToken;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<AuthenticationStateProvider, Guest>();
        services.AddSingleton<IHttpClientFactory, ArtResponses>();
        services.AddScoped<AuthenticatedApiClient>();
        services.AddScoped<SceneArtworkCache>();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new InteractiveRenderer(provider);
        var updates = Channel.CreateUnbounded<long>();
        var id = await renderer.Dispatcher.InvokeAsync(() => renderer.MountAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ArtCompositionView.ArtworkKey)] = "day1-house-flicker",
                [nameof(ArtCompositionView.Layers)] = DayOneArt.For("day1-house-flicker"),
                [nameof(ArtCompositionView.ArtworkDisplayed)] = EventCallback.Factory.Create<ArtworkSnapshot>(
                    this, _ => { Assert.True(updates.Writer.TryWrite(Stopwatch.GetTimestamp())); })
            })));

        var pending = await updates.Reader.ReadAsync(timeout.Token);
        Assert.Equal(("/images/art-generating.svg", 0UL), await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id)));

        Assert.True(await renderer.Dispatcher.InvokeAsync(() => renderer.HasText(id, "Preparing artwork…")));

        var completed = await updates.Reader.ReadAsync(timeout.Token);
        var ready = await renderer.Dispatcher.InvokeAsync(() => renderer.ImageFrame(id));
        Assert.True(Stopwatch.GetElapsedTime(pending, completed) >= TimeSpan.FromSeconds(4.9));
        Assert.Equal("/art/bg-day1-house-hallway/1/png", ready.Url);
        Assert.NotEqual(0UL, ready.ErrorHandler);
        Assert.False(await renderer.Dispatcher.InvokeAsync(() => renderer.HasText(id, "Preparing artwork…")));

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
        Assert.True(await renderer.Dispatcher.InvokeAsync(() => renderer.HasImage(id, "/art/light-day1-power-flicker/1/png")));

        await updates.Reader.ReadAsync(timeout.Token);
        Assert.True(await renderer.Dispatcher.InvokeAsync(() => renderer.HasImage(id, "/art/bg-day1-house-hallway/2/png")));
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
            new(this, disposeHandler: false)
            {
                BaseAddress = new Uri("http://test")
            };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.AbsolutePath != "/api/art/scene/resolve")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));

            var attempt = Interlocked.Increment(ref requests);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    Asset("bg-day1-house-hallway", attempt >= 2, attempt >= 4 ? 2 : 1),
                    Asset("light-day1-power-flicker", attempt >= 3)
                })
            });

            static object Asset(string key, bool ready, int version = 1) =>
                new { key, status = ready ? "ready" : "pending", version = ready ? version : (int?)null };
        }
    }

    private sealed class PrefetchResponses(string nextKey) : HttpMessageHandler, IHttpClientFactory
    {
        private int nextRequests;
        public TaskCompletionSource Revalidating { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Replacement { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new("http://test") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri?.AbsolutePath != "/api/art/scene/resolve")
                return new(HttpStatusCode.Forbidden);
            var payload = (await request.Content!.ReadFromJsonAsync<ResolveKeys>(ct))!;
            var version = 1;
            if (payload.Keys.Contains(nextKey) && Interlocked.Increment(ref nextRequests) > 1)
            {
                Revalidating.TrySetResult();
                version = nextRequests == 2 ? await Replacement.Task.WaitAsync(ct) : await WaitForCancellation(ct);
            }
            return new(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload.Keys.Select(key => new { key, status = "ready", version }))
            };
        }

        private static async Task<int> WaitForCancellation(CancellationToken ct)
        {
            await Task.Delay(Timeout.Infinite, ct);
            return 0;
        }

        private sealed record ResolveKeys(string[] Keys);
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

        public Task UpdateAsync(int id, ParameterView parameters) => RenderRootComponentAsync(id, parameters);

        public (string? Url, ulong ErrorHandler) ImageFrame(int id)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            for (var i = 0; i < frames.Count; i++)
            {
                var frame = frames.Array[i];
                if (frame.FrameType == RenderTreeFrameType.Markup &&
                    frame.MarkupContent.Contains("src=\"/images/art-generating.svg\"", StringComparison.Ordinal))
                    return ("/images/art-generating.svg", 0);
                if (frame is not { FrameType: RenderTreeFrameType.Element, ElementName: "img" })
                    continue;
                string? url = null;
                ulong handler = 0;
                for (var j = i + 1; j < frames.Count && frames.Array[j].FrameType == RenderTreeFrameType.Attribute; j++)
                {
                    if (frames.Array[j].AttributeName == "src")
                        url = frames.Array[j].AttributeValue as string;
                    if (frames.Array[j].AttributeName == "onerror")
                        handler = frames.Array[j].AttributeEventHandlerId;
                }
                return (url, handler);
            }
            return default;
        }

        public bool HasText(int id, string text) => HasFrame(id, frame =>
            frame.FrameType == RenderTreeFrameType.Text && frame.TextContent == text);

        public bool HasImage(int id, string url) => HasFrame(id, frame =>
            frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "src" &&
            Equals(frame.AttributeValue, url));

        private bool HasFrame(int id, Func<RenderTreeFrame, bool> predicate)
        {
            var frames = GetCurrentRenderTreeFrames(id);
            return frames.Array.Take(frames.Count).Any(predicate);
        }

        protected override void HandleException(Exception exception) => throw new InvalidOperationException("Interactive render failed.", exception);

        protected override Task UpdateDisplayAsync(in RenderBatch renderBatch) => Task.CompletedTask;
    }
#pragma warning restore BL0006
}
