using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fatewake.Web.Presentation;

public interface ISurvivorVisualResolver
{
    SurvivorVisualSnapshot Resolve(SurvivorVisualIdentity identity, SurvivorVisualState state);
    IReadOnlyList<ArtLayer> Compose(SurvivorVisualSnapshot snapshot, int baseZ, double x, double y, double width, double height);
}

public sealed class SurvivorVisualResolver : ISurvivorVisualResolver
{
    public SurvivorVisualSnapshot Resolve(SurvivorVisualIdentity identity, SurvivorVisualState state)
    {
        if (identity.SurvivorId != state.SurvivorId) throw new InvalidOperationException("Visual identity and state belong to different survivors.");
        var normalized = JsonSerializer.Serialize(new {
            identity.IdentityAssetKey, identity.IdentityAssetVersion, state.StateVersion, state.PoseKey,
            equipped = state.Equipped.OrderBy(x => x.Slot).ThenBy(x => x.Priority).ThenBy(x => x.AssetKey),
            traits = state.Traits.OrderBy(x => x.Key)
        });
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        return new(state.SurvivorId, state.StateVersion, fingerprint, DateTimeOffset.UtcNow, state);
    }

    public IReadOnlyList<ArtLayer> Compose(SurvivorVisualSnapshot snapshot, int baseZ, double x, double y, double width, double height)
    {
        var layers = new List<ArtLayer>();
        var ordered = snapshot.State.Equipped.OrderBy(e => SlotOrder(e.Slot)).ThenBy(e => e.Priority).ToArray();
        for (var i = 0; i < ordered.Length; i++)
            layers.Add(new(ordered[i].AssetKey, ordered[i].AssetVersion, baseZ + i, x, y, width, height));
        return layers;
    }

    private static int SlotOrder(VisualSlot slot) => slot switch {
        VisualSlot.Condition => 5, VisualSlot.Injury => 10, VisualSlot.Legs => 20, VisualSlot.Feet => 21,
        VisualSlot.TorsoBase => 30, VisualSlot.TorsoOuter => 40, VisualSlot.Back => 45, VisualSlot.Belt => 50,
        VisualSlot.PrimaryCarry => 60, VisualSlot.SecondaryCarry => 61, VisualSlot.Hands => 65,
        VisualSlot.Head => 70, VisualSlot.Face => 75, VisualSlot.Accessory => 80, VisualSlot.StoryMark => 90, _ => 50
    };
}
