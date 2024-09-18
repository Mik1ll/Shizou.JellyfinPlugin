using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin;

public class ImageProvider : IRemoteImageProvider
{
    private readonly Plugin _plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is null");

    public bool Supports(BaseItem item) => item is Movie or Series or Season;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var results = new List<RemoteImageInfo>();
        var animeId = item.GetProviderId(ProviderIds.Shizou);
        if (string.IsNullOrWhiteSpace(animeId))
            return Task.FromResult<IEnumerable<RemoteImageInfo>>(results);

        results.Add(new RemoteImageInfo()
        {
            ProviderName = Name,
            Url = $"api/Images/AnimePosters/{animeId}"
        });

        return Task.FromResult<IEnumerable<RemoteImageInfo>>(results);
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return await _plugin.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public string Name => "Shizou";
}
