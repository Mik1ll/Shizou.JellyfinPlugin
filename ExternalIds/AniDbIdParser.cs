using System.Text.RegularExpressions;

namespace Shizou.JellyfinPlugin.ExternalIds;

public static partial class AniDbIdParser
{
    public static string? IdFromString(string input) => IdRegex().Match(input).Groups[1].Value is var v && string.IsNullOrEmpty(v) ? null : v;

    [GeneratedRegex(@"\[anidb-([0-9]+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
}
