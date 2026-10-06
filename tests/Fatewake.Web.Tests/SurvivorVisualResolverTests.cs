using Fatewake.Web.Presentation;

namespace Fatewake.Web.Tests;

public sealed class SurvivorVisualResolverTests
{
    [Fact]
    public void Equipment_change_changes_visual_fingerprint_without_changing_identity()
    {
        var id=Guid.NewGuid();
        var identity=new SurvivorVisualIdentity(id,"survivor-face-darren-like-test",1);
        var resolver=new SurvivorVisualResolver();
        var baseState=new SurvivorVisualState(id,1,DateTimeOffset.UtcNow,"standing-front",
            [new(VisualSlot.TorsoOuter,"wear-jacket-canvas-brown-01",1),new(VisualSlot.PrimaryCarry,"tool-hatchet-basic-01",1)],new Dictionary<string,string>());
        var upgraded=baseState with { StateVersion=2, Equipped=[new(VisualSlot.TorsoOuter,"wear-jacket-canvas-brown-01",1),new(VisualSlot.PrimaryCarry,"tool-crowbar-red-worn-01",1)] };
        var before=resolver.Resolve(identity,baseState); var after=resolver.Resolve(identity,upgraded);
        Assert.NotEqual(before.Fingerprint,after.Fingerprint);
        Assert.Equal(before.SurvivorId,after.SurvivorId);
    }

    [Fact]
    public void Historical_snapshot_keeps_old_equipment()
    {
        var id=Guid.NewGuid(); var resolver=new SurvivorVisualResolver();
        var snapshot=resolver.Resolve(new(id,"identity-test",1),new(id,7,DateTimeOffset.UtcNow,"standing-front",
            [new(VisualSlot.PrimaryCarry,"tool-hatchet-basic-01",1)],new Dictionary<string,string>()));
        var layers=resolver.Compose(snapshot,20,.5,.5,.4,.7);
        Assert.Contains(layers,x=>x.AssetKey=="tool-hatchet-basic-01");
        Assert.DoesNotContain(layers,x=>x.AssetKey=="tool-crowbar-red-worn-01");
    }
}
