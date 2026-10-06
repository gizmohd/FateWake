using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fatewake.Web.Presentation;

public interface ISurvivorVisualResolver
{
    SurvivorVisualProjection Project(SurvivorVisualState state, SceneVisualContext context);
    SurvivorVisualSnapshot Snapshot(SurvivorVisualIdentity identity, SurvivorVisualProjection projection);
    IReadOnlyList<ArtLayer> Compose(SurvivorVisualSnapshot snapshot, int baseZ, double x, double y, double width, double height);
}

public sealed class SurvivorVisualResolver : ISurvivorVisualResolver
{
    public SurvivorVisualProjection Project(SurvivorVisualState state, SceneVisualContext context)
    {
        var visible = state.Loadout
            .Select(item => new VisibleVisual(item, EmphasisFor(item, context)))
            .Where(x => IsVisible(x, context))
            .OrderBy(x => SlotOrder(x.Source.Slot)).ThenBy(x => x.Source.Priority)
            .ToArray();
        return new(state.SurvivorId, state.StateVersion, context, visible);
    }

    public SurvivorVisualSnapshot Snapshot(SurvivorVisualIdentity identity, SurvivorVisualProjection projection)
    {
        if (identity.SurvivorId != projection.SurvivorId) throw new InvalidOperationException("Visual identity and projection belong to different survivors.");
        var normalized=JsonSerializer.Serialize(new {
            identity.IdentityAssetKey, identity.IdentityAssetVersion, projection.StateVersion,
            projection.Context.SceneKey, projection.Context.PoseKey, projection.Context.CameraKey,
            visible=projection.Visible.Select(x=>new{x.Source.AssetKey,x.Source.AssetVersion,x.Source.Slot,x.Source.ItemInstanceId,x.Emphasis})
        });
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return new(projection.SurvivorId,projection.StateVersion,fingerprint,DateTimeOffset.UtcNow,projection);
    }

    public IReadOnlyList<ArtLayer> Compose(SurvivorVisualSnapshot snapshot,int baseZ,double x,double y,double width,double height) =>
        snapshot.Projection.Visible.Select((v,i)=>new ArtLayer(v.Source.AssetKey,v.Source.AssetVersion,baseZ+i,x,y,width,height)).ToArray();

    private static VisualEmphasis EmphasisFor(EquippedVisual item, SceneVisualContext context) =>
        item.ItemInstanceId is not null && context.ItemEmphasis.TryGetValue(item.ItemInstanceId,out var emphasis) ? emphasis : VisualEmphasis.Normal;

    private static bool IsVisible(VisibleVisual item, SceneVisualContext context)
    {
        if (item.Emphasis==VisualEmphasis.Suppress) return false;
        if (item.Emphasis is VisualEmphasis.Reveal or VisualEmphasis.InHand) return true;
        if (item.Source.CarryVisibility is CarryVisibility.Concealed or CarryVisibility.Stored) return false;
        return item.Source.CompatiblePoses is null || item.Source.CompatiblePoses.Count==0 || item.Source.CompatiblePoses.Contains(context.PoseKey,StringComparer.OrdinalIgnoreCase);
    }

    private static int SlotOrder(VisualSlot slot)=>slot switch {
        VisualSlot.Condition=>5,VisualSlot.Injury=>10,VisualSlot.Legs=>20,VisualSlot.Feet=>21,VisualSlot.TorsoBase=>30,
        VisualSlot.TorsoOuter=>40,VisualSlot.Back=>45,VisualSlot.Belt=>50,VisualSlot.PrimaryCarry=>60,VisualSlot.SecondaryCarry=>61,
        VisualSlot.Hands=>65,VisualSlot.Head=>70,VisualSlot.Face=>75,VisualSlot.Accessory=>80,VisualSlot.StoryMark=>90,_=>50};
}
