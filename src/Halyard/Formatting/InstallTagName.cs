using System.Text;

namespace Halyard.Formatting;

/// <summary>
/// Turns Epic install tags into readable names: <c>GFP_BRCosmeticsInstallOnDemandFat</c> becomes
/// "BR Cosmetics Install On Demand Fat". Tags are build data, not UI text, so this is best effort.
/// </summary>
public static class InstallTagName
{
    // Unreal "Game Feature Plugin" prefix: noise for the user.
    private static readonly string[] NoisePrefixes = ["GFP_"];

    // Suffixes that only say where the content sits in the build.
    private static readonly string[] NoiseSuffixes = ["Root"];

    public static string Humanize(string tag)
    {
        var name = NoisePrefixes.Aggregate(tag, (current, prefix) =>
            current.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? current[prefix.Length..] : current);

        name = NoiseSuffixes.Aggregate(name, (current, suffix) =>
            current.Length > suffix.Length && current.EndsWith(suffix, StringComparison.Ordinal) ? current[..^suffix.Length] : current);

        return SplitWords(name.Replace('_', ' '));
    }

    /// <summary>Inserts spaces at word boundaries, keeping acronyms together ("BRCosmetics" -> "BR Cosmetics").</summary>
    private static string SplitWords(string text)
    {
        var builder = new StringBuilder(text.Length + 8);
        for (var i = 0; i < text.Length; i++)
        {
            var current = text[i];
            if (i > 0 && char.IsUpper(current) && builder[^1] != ' ')
            {
                var previousIsLower = char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1]);
                var endsAcronym = char.IsUpper(text[i - 1]) && i + 1 < text.Length && char.IsLower(text[i + 1]);
                if (previousIsLower || endsAcronym)
                {
                    builder.Append(' ');
                }
            }

            builder.Append(current);
        }

        return builder.ToString().Trim();
    }
}
