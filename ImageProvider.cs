using System.Text.RegularExpressions;
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

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if ((_plugin.ResilienceHandler.InnerHandler as SocketsHttpHandler)?.CookieContainer.Count == 0)
            await _plugin.ShizouHttpClient.LoginAsync(_plugin.Configuration.ServerPassword, cancellationToken).ConfigureAwait(false);
        var list = new List<RemoteImageInfo>();
        var animeIdStr = item.ProviderIds.GetValueOrDefault(ProviderIds.Shizou);
        if (string.IsNullOrWhiteSpace(animeIdStr))
            animeIdStr = Regex.Match(item.Name, @$"\[{ProviderIds.Shizou}-(\d+)\]") is { Success: true } reg ? reg.Groups[1].Value : null;
        if (!int.TryParse(animeIdStr, out var animeId))
            return list;

        list.Add(new RemoteImageInfo()
        {
            ProviderName = Name,
            Url = new Uri(_plugin.HttpClient.BaseAddress!, $"api/Images/AnimePosters/{animeId}").AbsoluteUri
        });

        return list;
    }

    public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        if ((_plugin.ResilienceHandler.InnerHandler as SocketsHttpHandler)?.CookieContainer.Count == 0)
            await _plugin.ShizouHttpClient.LoginAsync(_plugin.Configuration.ServerPassword, cancellationToken).ConfigureAwait(false);
        return await _plugin.HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public string Name => "Shizou";
}
