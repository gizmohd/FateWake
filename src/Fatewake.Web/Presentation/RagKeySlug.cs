using System.Text;

namespace Fatewake.Web.Presentation;

/// <summary>Derives default RAG source and timeline keys from a document title.</summary>
/// <remarks><see href="../../../docs/code/src/Fatewake.Web/Presentation/RagKeySlug.md">RagKeySlug documentation</see>.</remarks>
public static class RagKeySlug
{
    /// <summary>Lowercases the title, replaces whitespace runs with '-', and drops characters outside a-z, 0-9 and '-'; the result is truncated to <paramref name="maxLength"/>.</summary>
    public static string FromTitle(string? title, int maxLength)
    {
        var builder = new StringBuilder();
        var pendingDash = false;
        foreach (var character in (title ?? string.Empty).Trim().ToLowerInvariant())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingDash = builder.Length > 0;
                continue;
            }

            if (character is not ((>= 'a' and <= 'z') or (>= '0' and <= '9') or '-'))
                continue;
            if (pendingDash)
            {
                builder.Append('-');
                pendingDash = false;
            }

            builder.Append(character);
        }

        return builder.Length > maxLength ? builder.ToString(0, maxLength).TrimEnd('-') : builder.ToString();
    }
}
