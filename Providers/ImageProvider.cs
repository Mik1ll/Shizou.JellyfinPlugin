using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.Providers;

public class ImageProvider : IRemoteImageProvider
{
    private readonly ShizouClientManager _shizouClientManager;
    public bool Supports(BaseItem item) => item is Series or Episode or Person;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public ImageProvider(ShizouClientManager shizouClientManager)
    {
        _shizouClientManager = shizouClientManager;
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        List<RemoteImageInfo> result = [];
        switch (item)
        {
            case Episode ep when item.GetProviderId(ProviderIds.ShizouEp) is { } fileId && ep.Series.GetProviderId(ProviderIds.Shizou) is { } animeId:
            {
                var xrefs = (await _shizouClientManager.GetEpFileXrefsAsync(Convert.ToInt32(animeId), cancellationToken)
                    .ConfigureAwait(false))?.Where(xr => xr.AniDbFileId == Convert.ToInt32(fileId)).Select(xr => xr.AniDbEpisodeId).ToHashSet();
                if (xrefs is null)
                    break;
                var episodes = (await _shizouClientManager.GetEpisodesAsync(Convert.ToInt32(animeId), cancellationToken).ConfigureAwait(false))?
                    .Where(e => xrefs.Contains(e.Id)).ToList();
                var episodeId = episodes?.FirstOrDefault()?.Id;
                if (episodeId is not null)
                    result.Add(new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Url = $"api/Images/EpisodeThumbnails/{episodeId}",
                    });
                break;
            }
            case Series series when series.GetProviderId(ProviderIds.Shizou) is { } animeId:
            {
                result.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = $"api/Images/AnimePosters/{animeId}",
                });
                break;
            }
            case Person person when person.GetProviderId(ProviderIds.ShizouCreator) is { } creatorId:
            {
                result.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Url = $"api/Images/CreatorImages/{creatorId}",
                });
                break;
            }
        }

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _shizouClientManager.GetAsync(url, cancellationToken);

    public string Name => "Shizou";
}
