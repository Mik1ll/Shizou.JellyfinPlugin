using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.JellyfinPlugin.Extensions;

namespace Shizou.JellyfinPlugin.Providers;

public class ImageProvider : IRemoteImageProvider
{
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

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        Plugin.Instance.ShizouHttpClient.WithLoginRetry((_, ct) => Plugin.Instance.HttpClient.GetAsync(url, ct), cancellationToken);

    public string Name => "Shizou";
}
