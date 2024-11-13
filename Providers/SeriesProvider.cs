using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.JellyfinPlugin.Extensions;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
{
    public string Name => "Shizou";

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var animeId = info.GetProviderId(ProviderIds.Shizou) ?? AniDbIdParser.IdFromString(Path.GetFileName(info.Path));
        if (string.IsNullOrWhiteSpace(animeId))
            return new MetadataResult<Series>();

        var anime = await Plugin.Instance.ShizouHttpClient.WithLoginRetry(
            (sc, ct) => sc.AniDbAnimesGetAsync(Convert.ToInt32(animeId), ct),
            cancellationToken).ConfigureAwait(false);

        DateTimeOffset? airDateOffset = anime.AirDate is null ? null : new DateTimeOffset(anime.AirDate.Value.DateTime, TimeSpan.FromHours(9));
        DateTimeOffset? endDateOffset = anime.EndDate is null ? null : new DateTimeOffset(anime.EndDate.Value.DateTime, TimeSpan.FromHours(9));

        var result = new MetadataResult<Series>
        {
            Item = new Series
            {
                Name = anime.TitleTranscription,
                OriginalTitle = anime.TitleOriginal,
                PremiereDate = airDateOffset?.UtcDateTime,
                EndDate = endDateOffset?.UtcDateTime,
                Overview = anime.Description,
                HomePageUrl = $"https://anidb.net/anime/{animeId}",
                ProductionYear = airDateOffset?.Year,
                Status = airDateOffset <= DateTime.Now ? SeriesStatus.Ended :
                    airDateOffset > DateTime.Now ? SeriesStatus.Unreleased :
                    airDateOffset is not null && endDateOffset is not null ? SeriesStatus.Continuing : null,
                CommunityRating = anime.Rating,
                Tags = anime.Tags.ToArray(),
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.Shizou, animeId } }
            },
            HasMetadata = true
        };
        await AddPeople(result, Convert.ToInt32(animeId), cancellationToken).ConfigureAwait(false);

        return result;
    }

    private async Task AddPeople(MetadataResult<Series> result, int animeId, CancellationToken cancellationToken)
    {
        result.ResetPeople();
        var credits = await Plugin.Instance.ShizouHttpClient.AniDbCreditsByAniDbAnimeIdAsync(animeId, cancellationToken).ConfigureAwait(false);
        foreach (var credit in credits)
            result.AddPerson(new PersonInfo()
            {
                Name = credit.AniDbCreator.Name,
                Role = credit.AniDbCharacter?.Name ?? credit.Role,
                Type = credit.AniDbCharacterId is null ? PersonKind.Unknown : PersonKind.Actor,
                SortOrder = credit.AniDbCharacterId is not null
                    ? credit.Role switch
                    {
                        { } r when r.Contains("Main", StringComparison.OrdinalIgnoreCase) => 0,
                        { } r when r.Contains("Secondary", StringComparison.OrdinalIgnoreCase) => 1,
                        { } r when r.Contains("appears", StringComparison.OrdinalIgnoreCase) => 2,
                        _ => 3
                    }
                    : credit.Role switch
                    {
                        { } r when r.Contains("Original Work", StringComparison.OrdinalIgnoreCase) => 4,
                        { } r when r.Contains("Direction", StringComparison.OrdinalIgnoreCase) => 5,
                        { } r when r.Contains("Storyboard", StringComparison.OrdinalIgnoreCase) => 6,
                        { } r when r.Contains("Series Composition", StringComparison.OrdinalIgnoreCase) => 6,
                        { } r when r.Contains("Episode Direction", StringComparison.OrdinalIgnoreCase) => 7,
                        { } r when r.Contains("Character Design", StringComparison.OrdinalIgnoreCase) => 8,
                        _ => int.MaxValue
                    },
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.ShizouCreator, credit.AniDbCreator.Id.ToString() } }
            });
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
