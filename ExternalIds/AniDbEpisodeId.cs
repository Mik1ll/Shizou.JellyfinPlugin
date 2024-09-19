using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.ExternalIds;

public class AniDbEpisodeId : IExternalId
{
    public bool Supports(IHasProviderIds item) => item is Episode;

    public string ProviderName => "AniDB Episode";
    public string Key => ProviderIds.ShizouEp;
    public ExternalIdMediaType? Type => ExternalIdMediaType.Episode;
    public string? UrlFormatString => null;
}
