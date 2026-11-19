using Fatewake.GameEngine;
using Fatewake.GameEngine.DayOne;
using Fatewake.Web.Presentation;
namespace Fatewake.IntegrationTests;
/// <summary>Prevents canonical engine outcomes from falling through to a generic choice acknowledgment.</summary>
public sealed class DayOneOutcomePresentationTests
{
    /// <summary>Every supported action shows its specific persisted consequence.</summary>
    [Theory]
    [InlineData("help_injured_stranger", "immediate assistance")]
    [InlineData("call_from_safety", "doorway")]
    [InlineData("stay_inside", "no help")]
    [InlineData("investigate_radio", "tomorrow at 6:17 AM")]
    public void Canonical_engine_outcomes_have_concrete_player_visible_results(string action, string expected)
    {
        var state = new GameSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, "day-001-injured-stranger", new Dictionary<string, string>());
        var result = new DayOneGameEngine().Resolve(state, new CandidateAction(action, new Dictionary<string, string>()));
        Assert.True(result.Accepted);
        Assert.Contains(expected, DayOneOutcomes.Text(result.OutcomeKey, result.NarrativeFacts));
        Assert.DoesNotContain("world absorbs", DayOneOutcomes.Text(result.OutcomeKey, result.NarrativeFacts));
    }
}
