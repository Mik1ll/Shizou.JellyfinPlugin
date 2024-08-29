using System.Text.RegularExpressions;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.HttpClient;

namespace Shizou.JellyfinPlugin;

public class SeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IServerConfigurationManager _serverConfigurationManager;

    public SeriesProvider(IHttpClientFactory clientFactory, IServerConfigurationManager serverConfigurationManager)
    {
        _clientFactory = clientFactory;
        _serverConfigurationManager = serverConfigurationManager;
    }

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
    {
        var results = new List<RemoteSearchResult>();
        var animeIdStr = searchInfo.ProviderIds.GetValueOrDefault(ProviderIds.Shizou);
        if (int.TryParse(animeIdStr, out var animeId))
        {
            results.Add(new RemoteSearchResult());
            throw new NotImplementedException();
        }

        if (!string.IsNullOrWhiteSpace(searchInfo.Name))
        {
            var port = Plugin.Instance?.Configuration.ServerPort ?? 443;
            var uriBuilder = new UriBuilder("https", "localhost", port);
            var client = new ShizouHttpClient(uriBuilder.Uri.AbsoluteUri, _clientFactory.CreateClient());

            var aids = await client.GetAnimeSearchAsync(searchInfo.Name, cancellationToken).ConfigureAwait(false);

            results.AddRange(aids.Select(a => new RemoteSearchResult
            {
                Name = null,
                ImageUrl = null,
                ProviderIds = new Dictionary<string, string> { { ProviderIds.Shizou, a.ToString() } }
            }));
        }


        return results;
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var animeIdStr = info.ProviderIds.GetValueOrDefault(ProviderIds.Shizou);
        if (string.IsNullOrWhiteSpace(animeIdStr))
            animeIdStr = Regex.Match(info.Name, @$"\[{ProviderIds.Shizou}-(\d+)\]") is { Success: true } reg ? reg.Groups[1].Value : null;
        if (!int.TryParse(animeIdStr, out var animeId))
            return new MetadataResult<Series>();
        var port = Plugin.Instance?.Configuration.ServerPort ?? 443;
        var uriBuilder = new UriBuilder("https", "localhost", port);
        var client = new ShizouHttpClient(uriBuilder.Uri.AbsoluteUri, _clientFactory.CreateClient());
        var anime = await client.AniDbAnimesAsync(animeId, cancellationToken).ConfigureAwait(false);
        
        var res = new MetadataResult<Series>();
        res.Item.SetProviderId(ProviderIds.Shizou, animeId.ToString());

        DateTimeOffset? airDateTime = int.TryParse(anime.AirDate[..4], out var airY) && int.TryParse(anime.AirDate[5..7], out var airM) &&
                                      int.TryParse(anime.AirDate[8..10], out var airD)
            ? new DateTimeOffset(new DateTime(airY, airM, airD), TimeSpan.FromHours(9))
            : null;
        DateTimeOffset? endDateTime = int.TryParse(anime.EndDate[..4], out var endY) && int.TryParse(anime.EndDate[5..7], out var endM) &&
                                      int.TryParse(anime.EndDate[8..10], out var endD)
            ? new DateTimeOffset(new DateTime(endY, endM, endD), TimeSpan.FromHours(9))
            : null;
        res.Item = new Series
        {
            Name = anime.TitleTranscription,
            OriginalTitle = anime.TitleOriginal,
            PremiereDate = airDateTime?.UtcDateTime,
            EndDate = endDateTime?.UtcDateTime,
            Overview = anime.Description,
            HomePageUrl = $"https://anidb.net/anime/{animeId}",
            CommunityRating = null,
            ProductionYear = airY != 0 ? airY : null,
            DateLastMediaAdded = null,
            Status = endDateTime <= DateTime.Now ? SeriesStatus.Ended :
                airDateTime > DateTime.Now ? SeriesStatus.Unreleased :
                airDateTime is not null && endDateTime is not null ? SeriesStatus.Continuing : null
        };
        res.HasMetadata = true;

        return res;
    }

    public string Name => "Shizou";

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        using var client = _clientFactory.CreateClient();
        return await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }
}
