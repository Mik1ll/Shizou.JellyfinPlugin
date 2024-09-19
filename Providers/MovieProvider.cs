using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public class MovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>
{
    private readonly Plugin _plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is null");
    public string Name => "Shizou";

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var animeId = info.GetProviderId(ProviderIds.Shizou) ?? AniDbIdParser.IdFromString(Path.GetDirectoryName(info.Path) ?? "");
        if (string.IsNullOrWhiteSpace(animeId))
            return new MetadataResult<Movie>();

        var anime = await _plugin.ShizouHttpClient.AniDbAnimesAsync(Convert.ToInt32(animeId), cancellationToken).ConfigureAwait(false);


        DateTimeOffset? airDateTime = anime.AirDate is not null && int.TryParse(anime.AirDate[..4], out var airY) &&
                                      int.TryParse(anime.AirDate[5..7], out var airM) &&
                                      int.TryParse(anime.AirDate[8..10], out var airD)
            ? new DateTimeOffset(new DateTime(airY, airM, airD), TimeSpan.FromHours(9))
            : null;
        DateTimeOffset? endDateTime = anime.EndDate is not null && int.TryParse(anime.EndDate[..4], out var endY) &&
                                      int.TryParse(anime.EndDate[5..7], out var endM) &&
                                      int.TryParse(anime.EndDate[8..10], out var endD)
            ? new DateTimeOffset(new DateTime(endY, endM, endD), TimeSpan.FromHours(9))
            : null;

        var result = new MetadataResult<Movie>
        {
            Item = new Movie
            {
                Name = anime.TitleTranscription,
                OriginalTitle = anime.TitleOriginal,
                PremiereDate = airDateTime?.UtcDateTime,
                EndDate = endDateTime?.UtcDateTime,
                Overview = anime.Description,
                HomePageUrl = $"https://anidb.net/anime/{animeId}",
                ProductionYear = airDateTime?.Year,
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.Shizou, animeId } }
            },
            HasMetadata = true
        };

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _plugin.HttpClient.GetAsync(url, cancellationToken);

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
