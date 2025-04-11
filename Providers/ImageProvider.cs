using System.Net.Mime;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.Providers;

public class ImageProvider : IRemoteImageProvider, IHasItemChangeMonitor
{
    private readonly ShizouClientManager _shizouClientManager;
    private readonly IProviderManager _providerManager;
    public bool Supports(BaseItem item) => item is Series or Episode or Person;
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => [ImageType.Primary];

    public ImageProvider(ShizouClientManager shizouClientManager, IProviderManager providerManager)
    {
        _shizouClientManager = shizouClientManager;
        _providerManager = providerManager;
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
                    result.Add(RemoteImageFromUri($"api/Images/EpisodeThumbnails/{episodeId}"));
                break;
            }
            case Series series when series.GetProviderId(ProviderIds.Shizou) is { } animeId:
            {
                result.Add(RemoteImageFromUri($"api/Images/AnimePosters/{animeId}"));
                break;
            }
            case Person person when person.GetProviderId(ProviderIds.ShizouCreator) is { } creatorId:
            {
                // Get image immediately. For some reason this.GetImageResponse isn't used for some item types such as Person
                var response = await _shizouClientManager.GetCreatorImageAsync(Convert.ToInt32(creatorId), cancellationToken).ConfigureAwait(false);
                if (response is { } resp)
                {
                    var stream = new MemoryStream(resp.img);
                    await using (stream.ConfigureAwait(false))
                        await _providerManager.SaveImage(
                            item,
                            stream,
                            resp.mimeType,
                            ImageType.Primary,
                            null,
                            cancellationToken
                        ).ConfigureAwait(false);
                }

                break;
            }
        }

        return result;
    }

    private RemoteImageInfo RemoteImageFromUri(string relativeUri) => new()
    {
        ProviderName = Name,
        Url = new Uri(new Uri(Plugin.Instance.Configuration.ServerBaseAddress), relativeUri).AbsoluteUri,
    };

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _shizouClientManager.GetAsync(url, cancellationToken);

    public string Name => "Shizou";
    public bool HasChanged(BaseItem item, IDirectoryService directoryService) => true;
}
