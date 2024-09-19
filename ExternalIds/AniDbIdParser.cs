using System.Text.RegularExpressions;

namespace Shizou.JellyfinPlugin.ExternalIds;

public static class AniDbIdParser
{
    private static readonly Regex IdRegex = new(@"\[anidb-(\d+)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? IdFromString(string input) => IdRegex.Match(input).Groups[1].Value;
}
