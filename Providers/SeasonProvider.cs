using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.Providers;

public class SeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
{
    public string Name => "Shizou";

    public Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
    {
        var seasonNumber = info.IndexNumber == 0 ? 0 : 1;

        var result = new MetadataResult<Season>
        {
            HasMetadata = true,
            Item = new Season
            {
                Name = seasonNumber == 0 ? "Specials" : "Episodes",
                IndexNumber = seasonNumber,
            },
        };

        return Task.FromResult(result);
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
