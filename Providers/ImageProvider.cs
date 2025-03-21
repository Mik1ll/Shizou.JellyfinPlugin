using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.Providers;

public class ImageProvider : IRemoteImageProvider
{
    private readonly ShizouClientManager _shizouClientManager;
    public bool Supports(BaseItem item) => item is Movie or Series or Season or Episode or Person;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public ImageProvider(ShizouClientManager shizouClientManager)
    {
        _shizouClientManager = shizouClientManager;
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var animeId = item.GetProviderId(ProviderIds.Shizou);
        var fileId = item.GetProviderId(ProviderIds.ShizouEp);
        var creatorId = item.GetProviderId(ProviderIds.ShizouCreator);
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            var episodes = await _shizouClientManager.ShizouHttpClient.AniDbEpisodesByAniDbFileIdAsync(Convert.ToInt32(fileId), cancellationToken)
                .ConfigureAwait(false);
            var episodeId = episodes.FirstOrDefault()?.Id;
            if (episodeId is not null)
                return
                [
                    new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Url = $"api/Images/EpisodeThumbnails/{episodeId}",
                    },
                ];
        }

        if (!string.IsNullOrWhiteSpace(animeId))
            return
            [
                new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = $"api/Images/AnimePosters/{animeId}",
                },
            ];

        if (!string.IsNullOrWhiteSpace(creatorId))
            return
            [
                new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = $"api/Images/CreatorImages/{creatorId}",
                },
            ];
        return [];
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _shizouClientManager.WithLoginRetry(_ => _shizouClientManager.HttpClient.GetAsync(url, cancellationToken), cancellationToken);

    public string Name => "Shizou";
}
