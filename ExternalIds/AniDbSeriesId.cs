using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace Shizou.JellyfinPlugin.ExternalIds;

public class AniDbSeriesId : IExternalId
{
    public bool Supports(IHasProviderIds item) => item is Series or Movie;

    public string ProviderName => "AniDB Series";
    public string Key => ProviderIds.Shizou;
    public ExternalIdMediaType? Type => ExternalIdMediaType.Series;
    public string? UrlFormatString => null;
}
