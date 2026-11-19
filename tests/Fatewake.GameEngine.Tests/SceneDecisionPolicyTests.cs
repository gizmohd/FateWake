namespace Fatewake.GameEngine.Tests;

/// <summary>Verifies the four-scene decision-free pacing rule.</summary>
public sealed class SceneDecisionPolicyTests
{
    /// <summary>Four decision-free scenes are accepted and the fifth is rejected without a decision.</summary>
    [Fact]
    public void Fifth_consecutive_scene_requires_a_decision()
    {
        var streak = 0;
        for (var i = 0; i < 4; i++)
        {
            Assert.True(SceneDecisionPolicy.TryAccept(streak, false, out streak));
        }

        Assert.Equal(4, streak);
        Assert.True(SceneDecisionPolicy.RequiresDecision(streak));
        Assert.False(SceneDecisionPolicy.TryAccept(streak, false, out _));
        Assert.True(SceneDecisionPolicy.TryAccept(streak, true, out streak));
        Assert.Equal(0, streak);
    }

    /// <summary>An early decision resets the streak.</summary>
    [Fact]
    public void Early_decision_resets_the_streak()
    {
        Assert.True(SceneDecisionPolicy.TryAccept(2, true, out var streak));
        Assert.Equal(0, streak);
    }
}
