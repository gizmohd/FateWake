using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Fatewake.Infrastructure.Art;
/// <summary>Creates deterministic cache identities for a base visual identity and normalized appearance combination.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Infrastructure/Art/CharacterAppearanceFingerprint.md">CharacterAppearanceFingerprint documentation</see>.</remarks>
public static class CharacterAppearanceFingerprint
{
    /// <summary>Calculates a stable SHA-256 fingerprint from the base identity version and normalized appearance state.</summary>
    public static string Create(string baseIdentityKey,int baseIdentityVersion,CharacterAppearance appearance)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseIdentityKey);
        var normalized=new
        {
            BaseIdentityKey=baseIdentityKey.Trim().ToLowerInvariant(),
            BaseIdentityVersion=baseIdentityVersion,
            HairStyle=Normalize(appearance.HairStyle),
            HairColor=Normalize(appearance.HairColor),
            EyeColor=Normalize(appearance.EyeColor),
            FacialHairStyle=Normalize(appearance.FacialHairStyle),
            FacialHairColor=Normalize(appearance.FacialHairColor),
            AdditionalTraits=Normalize(appearance.AdditionalTraits)
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(normalized)))).ToLowerInvariant();
    }

    private static string? Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim().ToLowerInvariant();
}