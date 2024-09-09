using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin;

public class SeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
{
    private readonly Plugin _plugin;
    private readonly SeriesProvider _seriesProvider;

    public SeasonProvider()
    {
        _plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is null");
        _seriesProvider = new SeriesProvider();
    }

    public string Name { get; } = "Shizou";

    public Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
    {
        var res = new MetadataResult<Season>()
        {
            HasMetadata = true,
            Item = new Season()
            {
                Name = "Episodes"
            }
        };

        return Task.FromResult(res);
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        await _plugin.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
