namespace Fatewake.Web.Presentation;

public enum BeatKind { Establishing, Narration, Dialogue, Interaction, Consequence, Anomaly, Transition }

public sealed record SceneInteraction(string ActionType,string Label,bool AllowFreeform=false);

public sealed record BeatDefinition(
    string Key,
    BeatKind Kind,
    string ArtworkKey,
    string? Time,
    string? Status,
    string? Narration,
    DialogueLine? Dialogue,
    IReadOnlyList<SceneInteraction> Interactions,
    bool AutoAdvance=false,
    int AutoAdvanceMilliseconds=0);

public sealed record SceneDefinition(string Key,string EpisodeKey,int SurvivorDay,string Title,IReadOnlyList<BeatDefinition> Beats)
{
    public BeatDefinition Beat(string key)=>Beats.FirstOrDefault(x=>x.Key==key) ?? Beats[0];
    public int IndexOf(string key){for(var i=0;i<Beats.Count;i++) if(Beats[i].Key==key)return i;return 0;}
}
