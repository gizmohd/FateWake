using Fatewake.GameEngine.DayOne;

namespace Fatewake.GameEngine.Tests;

public sealed class DayOneGameEngineTests
{
    [Fact]
    public void Helping_the_injured_stranger_creates_a_persistent_first_impression_wake()
    {
        var engine = new DayOneGameEngine();
        var state = new GameSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, "day-001-injured-stranger", new Dictionary<string,string>());

        var result = engine.Resolve(state, new CandidateAction("help_injured_stranger", new Dictionary<string,string>()));

        Assert.True(result.Accepted);
        Assert.Equal("injured_stranger_helped", result.OutcomeKey);
        Assert.Contains(result.Wakes, x => x.Type == "first_impression" && x.Target == "michelle");
    }

    [Fact]
    public void Unsupported_actions_cannot_change_canonical_state()
    {
        var engine = new DayOneGameEngine();
        var state = new GameSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, "day-001-injured-stranger", new Dictionary<string,string>());

        var result = engine.Resolve(state, new CandidateAction("teleport_everyone_to_safety", new Dictionary<string,string>()));

        Assert.False(result.Accepted);
        Assert.Empty(result.Effects);
        Assert.Empty(result.Wakes);
    }
}
