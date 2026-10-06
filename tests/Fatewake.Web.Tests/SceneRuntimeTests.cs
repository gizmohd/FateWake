using Fatewake.Web.Presentation;

namespace Fatewake.Web.Tests;

public sealed class SceneRuntimeTests
{
    [Fact]
    public void Day_one_opening_has_expected_vertical_slice()
    {
        var scene=DayOneScenes.Opening;
        Assert.Equal("day1-0617-0643",scene.Key);
        Assert.Equal(9,scene.Beats.Count);
        Assert.Equal("phone-0617",scene.Beats[0].Key);
        Assert.Equal("silence",scene.Beats[^1].Key);
    }

    [Fact]
    public void Only_first_choice_is_an_interaction_beat()
    {
        var interactions=DayOneScenes.Opening.Beats.Where(x=>x.Kind==BeatKind.Interaction).ToArray();
        Assert.Single(interactions);
        Assert.Equal("first-choice",interactions[0].Key);
        Assert.Contains(interactions[0].Interactions,x=>x.ActionType=="freeform");
    }

    [Fact]
    public void Every_day_one_beat_has_an_artwork_key()
    {
        Assert.All(DayOneScenes.Opening.Beats,x=>Assert.False(string.IsNullOrWhiteSpace(x.ArtworkKey)));
    }
}
