using Fatewake.Web.Components.Presentation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace Fatewake.Web.Tests;
/// <summary>Checks history rendering uses pinned images without any API or generation dependencies.</summary>
public sealed class NovelPanelTests
{
    /// <summary>Page breaks preserve chronological order and isolate disclosed cutscenes and chapters.</summary>
    [Fact]
    public void Page_templates_preserve_order_and_cutscene_boundaries()
    {
        var panels = Enumerable.Range(1, 10).Select(i => new NovelPanel(Guid.NewGuid(), i, i < 9 ? 1 : 2,
            i == 6 ? "cutscene" : "scene", "Moment " + i, null, "Remembered narration", null, null, [], false)).ToArray();
        var pages = Fatewake.Web.Presentation.NovelPage.Arrange(panels.Reverse());
        Assert.Equal(new[] { 4, 1, 1, 2, 2 }, pages.Select(x => x.Panels.Length));
        Assert.Equal(panels.Select(x => x.Id), pages.SelectMany(x => x.Panels).Select(x => x.Id));
        Assert.Equal("cutscene", pages[2].Panels.Single().Kind);
        Assert.All(pages, p => Assert.All(p.Panels, panel => Assert.Equal(p.SurvivorDay, panel.SurvivorDay)));
        Assert.Equal(Enumerable.Range(1, 5), pages.Select(x => x.Number));
    }
    /// <summary>Panels preserve asset versions and cutscene narration without interactive choices.</summary>
    [Fact]
    public async Task Reader_renders_saved_art_and_cutscene_without_generation_services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var panel = new NovelPanel(Guid.NewGuid(), 1, 1, "cutscene", "A turn of fate", null, "The group reaches shelter.", null, null,
            [new ArtLayer("bg-day1-bedroom-dark", 7, 0)], false);
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<NovelPanelView>(ParameterView.FromDictionary(new Dictionary<string, object?> { ["Panel"] = panel }));
            return result.ToHtmlString();
        });
        Assert.Contains("A TURN OF FATE", html);
        Assert.Contains("The group reaches shelter.", html);
        Assert.Contains("/art/bg-day1-bedroom-dark/7/png", html);
        Assert.DoesNotContain("button", html);
        Assert.DoesNotContain("Preparing artwork", html);
        var toast = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<StoryToastView>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                ["Notice"] = new StoryNotice(panel.Id, 2, "Your story is clearer", "Another survivor unlocked the gate.")
            }));
            return result.ToHtmlString();
        });
        Assert.Contains("role=\"status\"", toast);
        Assert.Contains("Your story is clearer", toast);
        Assert.Contains("Read the story", toast);
        Assert.Contains("Dismiss", toast);
        Assert.Contains("unlocked the gate", toast);
    }
}
