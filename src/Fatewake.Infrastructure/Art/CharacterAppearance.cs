namespace Fatewake.Infrastructure.Art;
/// <summary>Defines normalized mutable visual traits applied to a stable character identity.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/CharacterAppearance.md">CharacterAppearance documentation</see>.</remarks>
public sealed record CharacterAppearance(string? HairStyle=null,string? HairColor=null,string? EyeColor=null,string? FacialHairStyle=null,string? FacialHairColor=null,string? AdditionalTraits=null);