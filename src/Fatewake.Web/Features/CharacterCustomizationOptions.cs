namespace Fatewake.Web.Features;
/// <summary>Controls whether character appearance customization is exposed by the application.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Features/CharacterCustomizationOptions.md">CharacterCustomizationOptions documentation</see>.</remarks>
public sealed class CharacterCustomizationOptions
{
 public const string SectionName="Features:CharacterCustomization";
 /// <summary>Gets or sets whether character customization exists for users. Defaults to false.</summary>
 public bool Enabled{get;set;}
}