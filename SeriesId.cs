using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace Shizou.JellyfinPlugin;

public class SeriesId : IExternalId
{
    public bool Supports(IHasProviderIds item) => item is Series or Movie;

    public string ProviderName => "Shizou Series";
    public string Key => ProviderIds.Shizou;
    public ExternalIdMediaType? Type => null;
    public string? UrlFormatString => null;
}
