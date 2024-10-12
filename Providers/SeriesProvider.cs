using System.Globalization;
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
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.Shizou, animeId } }
            },
            HasMetadata = true
        };

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
