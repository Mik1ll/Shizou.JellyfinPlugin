using System.Text.RegularExpressions;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public partial class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
{
    private readonly ShizouClientManager _shizouClientManager;

    [GeneratedRegex(@"(https?:\/\/\S*?) \[(.+?)\]")]
    public static partial Regex LinkRegex();

    public static string AniDbLinksToMarkDown(string text) => LinkRegex().Replace(text, match => $"[{match.Groups[2]}]({match.Groups[1]})");

    public SeriesProvider(ShizouClientManager shizouClientManager) => _shizouClientManager = shizouClientManager;

    public string Name => "Shizou";

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var animeId = info.GetProviderId(ProviderIds.Shizou) ?? AniDbIdParser.IdFromString(Path.GetFileName(info.Path));
        if (string.IsNullOrWhiteSpace(animeId))
            return new MetadataResult<Series>();

        var anime = await _shizouClientManager.GetAnimeAsync(Convert.ToInt32(animeId), cancellationToken).ConfigureAwait(false);
        if (anime is null)
            return new MetadataResult<Series>();

        DateTimeOffset? airDateOffset = anime.AirDate is null ? null : new DateTimeOffset(anime.AirDate.Value.DateTime, TimeSpan.FromHours(9));
        DateTimeOffset? endDateOffset = anime.EndDate is null ? null : new DateTimeOffset(anime.EndDate.Value.DateTime, TimeSpan.FromHours(9));

        var overview = anime.Description is null ? null : AniDbLinksToMarkDown(anime.Description);

        var result = new MetadataResult<Series>
        {
            Item = new Series
            {
                Name = anime.TitleTranscription,
                OriginalTitle = anime.TitleOriginal,
                PremiereDate = airDateOffset?.UtcDateTime,
                EndDate = endDateOffset?.UtcDateTime,
                Overview = overview,
                HomePageUrl = $"https://anidb.net/anime/{animeId}",
                ProductionYear = airDateOffset?.Year,
                Status = airDateOffset <= DateTime.Now ? SeriesStatus.Ended :
                    airDateOffset > DateTime.Now ? SeriesStatus.Unreleased :
                    airDateOffset is not null && endDateOffset is not null ? SeriesStatus.Continuing : null,
                CommunityRating = anime.Rating,
                Tags = anime.Tags.ToArray(),
                ProviderIds = new Dictionary<string, string> { { ProviderIds.Shizou, animeId } },
            },
            HasMetadata = true,
        };
        await AddPeople(result, Convert.ToInt32(animeId), cancellationToken).ConfigureAwait(false);

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);

    private async Task AddPeople(MetadataResult<Series> result, int animeId, CancellationToken cancellationToken)
    {
        result.ResetPeople();
        var credits = await _shizouClientManager.GetCreditsAsync(animeId, cancellationToken).ConfigureAwait(false);
        if (credits is null)
            return;
        foreach (var credit in credits.Where(c => c.AniDbCreator.Type is CreatorType.Person))
            if (!string.IsNullOrWhiteSpace(credit.AniDbCreator.Name))
                result.AddPerson(new PersonInfo
                {
                    Name = credit.AniDbCreator.Name,
                    Role = credit.AniDbCharacter?.Name ?? credit.Role,
                    Type = credit.AniDbCharacterId is null
                        ? credit.Role switch
                        {
                            { } r when r.Contains("Producer", StringComparison.OrdinalIgnoreCase) => PersonKind.Producer,
                            { } r when r.Contains("Direct", StringComparison.OrdinalIgnoreCase) => PersonKind.Director,
                            { } r when r.Contains("Original", StringComparison.OrdinalIgnoreCase) => PersonKind.Author,
                            { } r when r.Contains("Sound", StringComparison.OrdinalIgnoreCase) => PersonKind.Engineer,
                            { } r when r.Contains("Edit", StringComparison.OrdinalIgnoreCase) => PersonKind.Editor,
                            { } r when r.Contains("Lyric", StringComparison.OrdinalIgnoreCase) => PersonKind.Lyricist,
                            { } r when r.Contains("Colo", StringComparison.OrdinalIgnoreCase) => PersonKind.Colorist,
                            { } r when r.Contains("Character", StringComparison.OrdinalIgnoreCase) => PersonKind.Illustrator,
                            { } r when r.Contains("Writ", StringComparison.OrdinalIgnoreCase) ||
                                       r.Contains("Script", StringComparison.OrdinalIgnoreCase) => PersonKind.Writer,
                            { } r when r.Contains("Arrang", StringComparison.OrdinalIgnoreCase) => PersonKind.Arranger,
                            { } r when r.Contains("Compos", StringComparison.OrdinalIgnoreCase) => PersonKind.Composer,
                            { } r when r.Contains("Music", StringComparison.OrdinalIgnoreCase) => PersonKind.Artist,
                            { } r when r.Contains("Mix", StringComparison.OrdinalIgnoreCase) => PersonKind.Mixer,
                            _ => PersonKind.Creator,
                        }
                        : PersonKind.Actor,
                    SortOrder = credit.AniDbCharacterId is not null
                        ? credit.Role switch
                        {
                            { } r when r.Contains("Main", StringComparison.OrdinalIgnoreCase) => 0,
                            { } r when r.Contains("Secondary", StringComparison.OrdinalIgnoreCase) => 1,
                            { } r when r.Contains("appears", StringComparison.OrdinalIgnoreCase) => 2,
                            _ => 3,
                        }
                        : credit.Role switch
                        {
                            { } r when r.Contains("Original Work", StringComparison.OrdinalIgnoreCase) => 4,
                            { } r when r.Contains("Direction", StringComparison.OrdinalIgnoreCase) => 5,
                            { } r when r.Contains("Storyboard", StringComparison.OrdinalIgnoreCase) => 6,
                            { } r when r.Contains("Series Composition", StringComparison.OrdinalIgnoreCase) => 6,
                            { } r when r.Contains("Episode Direction", StringComparison.OrdinalIgnoreCase) => 7,
                            { } r when r.Contains("Character Design", StringComparison.OrdinalIgnoreCase) => 8,
                            _ => int.MaxValue,
                        },
                    ProviderIds = new Dictionary<string, string> { { ProviderIds.ShizouCreator, credit.AniDbCreator.Id.ToString() } },
                });
    }
}
