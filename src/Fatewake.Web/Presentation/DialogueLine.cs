namespace Fatewake.Web.Presentation;

/// <summary>Represents a line of character or source-attributed dialogue.</summary>
/// <param name="Speaker">Speaker label shown with the line.</param>
/// <param name="Text">Dialogue text.</param>
/// <see href="../../../docs/code/src/Fatewake.Web/Presentation/DialogueLine.md">DialogueLine documentation</see>
public sealed record DialogueLine(string Speaker, string Text);
