using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.ExternalIds;

public class AniDbCreatorId : IExternalId
{
    public bool Supports(IHasProviderIds item) => item is Person;

    public string ProviderName => "AniDB";
    public string Key => ProviderIds.ShizouCreator;
    public ExternalIdMediaType? Type => ExternalIdMediaType.Person;
    public string UrlFormatString => "https://anidb.net/creator/{0}";
}
