using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.ExternalIds;

public class AniDbFileId : IExternalId
{
    public bool Supports(IHasProviderIds item) => item is Episode;

    public string ProviderName => "AniDB File";
    public string Key => ProviderIds.ShizouEp;
    public ExternalIdMediaType? Type => null;
    public string UrlFormatString => "https://anidb.net/file/{0}";
}
