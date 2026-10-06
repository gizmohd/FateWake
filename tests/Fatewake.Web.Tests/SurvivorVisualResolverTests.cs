using Fatewake.Web.Presentation;

namespace Fatewake.Web.Tests;

public sealed class SurvivorVisualResolverTests
{
    [Fact]
    public void Stored_and_concealed_items_are_not_rendered_by_default()
    {
        var id=Guid.NewGuid(); var resolver=new SurvivorVisualResolver();
        var state=new SurvivorVisualState(id,1,DateTimeOffset.UtcNow,"standing-front",[
            new(VisualSlot.Back,"wear-backpack-daypack-gray-01",1,"pack",CarryVisibility:CarryVisibility.AlwaysVisible),
            new(VisualSlot.Belt,"tool-flashlight-01",1,"flashlight",CarryVisibility:CarryVisibility.Concealed),
            new(VisualSlot.PrimaryCarry,"tool-hatchet-basic-01",1,"hatchet",CarryVisibility:CarryVisibility.Stored)
        ],new Dictionary<string,string>());
        var context=new SceneVisualContext("street","standing-front","medium",new Dictionary<string,VisualEmphasis>());
        var projection=resolver.Project(state,context);
        Assert.Single(projection.Visible);
        Assert.Equal("wear-backpack-daypack-gray-01",projection.Visible[0].Source.AssetKey);
    }

    [Fact]
    public void Story_action_can_reveal_or_put_an_item_in_hand()
    {
        var id=Guid.NewGuid(); var resolver=new SurvivorVisualResolver();
        var state=new SurvivorVisualState(id,2,DateTimeOffset.UtcNow,"standing-front",[
            new(VisualSlot.PrimaryCarry,"tool-hatchet-basic-01",1,"hatchet",CarryVisibility:CarryVisibility.Stored)
        ],new Dictionary<string,string>());
        var context=new SceneVisualContext("shed","standing-front","medium",
            new Dictionary<string,VisualEmphasis>{{"hatchet",VisualEmphasis.InHand}});
        Assert.Single(resolver.Project(state,context).Visible);
    }

    [Fact]
    public void Historical_snapshot_fingerprints_only_the_projected_appearance()
    {
        var id=Guid.NewGuid(); var resolver=new SurvivorVisualResolver(); var identity=new SurvivorVisualIdentity(id,"identity-test",1);
        var state=new SurvivorVisualState(id,7,DateTimeOffset.UtcNow,"standing-front",[
            new(VisualSlot.Back,"pack-01",1,"pack",CarryVisibility:CarryVisibility.AlwaysVisible),
            new(VisualSlot.PrimaryCarry,"tool-hatchet-basic-01",1,"hatchet",CarryVisibility:CarryVisibility.Stored)
        ],new Dictionary<string,string>());
        var context=new SceneVisualContext("street","standing-front","wide",new Dictionary<string,VisualEmphasis>());
        var snapshot=resolver.Snapshot(identity,resolver.Project(state,context));
        Assert.Contains(snapshot.Projection.Visible,x=>x.Source.AssetKey=="pack-01");
        Assert.DoesNotContain(snapshot.Projection.Visible,x=>x.Source.AssetKey=="tool-hatchet-basic-01");
    }
}
