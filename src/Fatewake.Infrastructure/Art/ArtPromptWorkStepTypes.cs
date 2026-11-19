namespace Fatewake.Infrastructure.Art;

/// <summary>Names durable prompt-authoring operations handled by the artwork worker.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/ArtPromptWorkStepTypes.md">Documentation</see>.</remarks>
public static class ArtPromptWorkStepTypes
{
    /// <summary>Durable step handled by the prompt-authoring worker.</summary>
    public const string Author = "art.prompt-author";
}
