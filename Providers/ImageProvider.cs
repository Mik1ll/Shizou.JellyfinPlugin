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
    public bool Supports(BaseItem item) => item is Movie or Series or Season or Episode or Person;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary, ImageType.Thumb];

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var animeId = item.GetProviderId(ProviderIds.Shizou);
        var fileId = item.GetProviderId(ProviderIds.ShizouEp);
        var creatorId = item.GetProviderId(ProviderIds.ShizouCreator);
        if (!string.IsNullOrWhiteSpace(fileId))
        {
            var episodes = await Plugin.Instance.ShizouHttpClient.AniDbEpisodesByAniDbFileIdAsync(Convert.ToInt32(fileId), cancellationToken)
                .ConfigureAwait(false);
            var episodeId = episodes.FirstOrDefault()?.Id;
            if (episodeId is not null)
                return
                [
                    new RemoteImageInfo()
                    {
                        ProviderName = Name,
                        Url = $"api/Images/EpisodeThumbnails/{episodeId}"
                    }
                ];
        }

        if (!string.IsNullOrWhiteSpace(animeId))
            return
            [
                new RemoteImageInfo()
                {
                    ProviderName = Name,
                    Url = $"api/Images/AnimePosters/{animeId}"
                }
            ];

        if (!string.IsNullOrWhiteSpace(creatorId))
            return
            [
                new RemoteImageInfo()
                {
                    ProviderName = Name,
                    Url = $"api/Images/CreatorImages/{creatorId}"
                }
            ];
        return [];
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        Plugin.Instance.ShizouHttpClient.WithLoginRetry((_, ct) => Plugin.Instance.HttpClient.GetAsync(url, ct), cancellationToken);

    public string Name => "Shizou";
}
