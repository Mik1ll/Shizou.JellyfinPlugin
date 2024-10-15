using System.Globalization;
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


        DateTimeOffset? airDateOffset, endDateOffset;
        {
            if (!DateTime.TryParseExact(anime.AirDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var airDateTime))
                if (!DateTime.TryParseExact(anime.AirDate, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out airDateTime))
                    DateTime.TryParseExact(anime.AirDate, "yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out airDateTime);
            airDateOffset = airDateTime == DateTime.MinValue ? null : new DateTimeOffset(airDateTime, TimeSpan.FromHours(9));

            if (!DateTime.TryParseExact(anime.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDateTime))
                if (!DateTime.TryParseExact(anime.EndDate, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDateTime))
                    DateTime.TryParseExact(anime.EndDate, "yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDateTime);
            endDateOffset = endDateTime == DateTime.MinValue ? null : new DateTimeOffset(endDateTime, TimeSpan.FromHours(9));
        }

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
                        var r when r.Contains("Main", StringComparison.OrdinalIgnoreCase) => 0,
                        var r when r.Contains("Secondary", StringComparison.OrdinalIgnoreCase) => 1,
                        var r when r.Contains("appears", StringComparison.OrdinalIgnoreCase) => 2,
                        _ => 3
                    }
                    : credit.Role switch
                    {
                        var r when r.Contains("Original Work", StringComparison.OrdinalIgnoreCase) => 4,
                        var r when r.Contains("Direction", StringComparison.OrdinalIgnoreCase) => 5,
                        var r when r.Contains("Storyboard", StringComparison.OrdinalIgnoreCase) => 6,
                        var r when r.Contains("Series Composition", StringComparison.OrdinalIgnoreCase) => 6,
                        var r when r.Contains("Episode Direction", StringComparison.OrdinalIgnoreCase) => 7,
                        var r when r.Contains("Character Design", StringComparison.OrdinalIgnoreCase) => 8,
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
