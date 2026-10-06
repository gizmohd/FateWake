namespace Fatewake.Web.Presentation;

/// <summary>Pairs an equipped visual with its resolved scene-specific emphasis.</summary>
/// <param name="Source">Original equipped visual definition.</param>
/// <param name="Emphasis">Resolved visibility override.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/VisibleVisual.md">VisibleVisual documentation</see>
public sealed record VisibleVisual(EquippedVisual Source, VisualEmphasis Emphasis);
