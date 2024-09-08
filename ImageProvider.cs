using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin;

public class ImageProvider : IRemoteImageProvider
{
    private readonly Plugin _plugin;

    public ImageProvider() => _plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is null");

    public bool Supports(BaseItem item) => item is Movie or Series or Season;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var list = new List<RemoteImageInfo>();
        var animeIdStr = item.ProviderIds.GetValueOrDefault(ProviderIds.Shizou);
        if (!int.TryParse(animeIdStr, out var animeId))
            return Task.FromResult<IEnumerable<RemoteImageInfo>>(list);

        list.Add(new RemoteImageInfo()
        {
            ProviderName = Name,
            Url = new Uri(_plugin.HttpClient.BaseAddress!, $"api/Images/AnimePosters/{animeId}").AbsoluteUri
        });

        return Task.FromResult<IEnumerable<RemoteImageInfo>>(list);
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return await _plugin.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public string Name => "Shizou";
}
