using System.Text.RegularExpressions;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Shizou.HttpClient;

namespace Shizou.JellyfinPlugin;

public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private readonly Plugin _plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is null");

    public string Name => "Shizou";

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var epIdStr = info.ProviderIds.GetValueOrDefault(ProviderIds.ShizouEp);
        if (string.IsNullOrWhiteSpace(epIdStr))
            epIdStr = Regex.Match(Path.GetFileName(info.Path), @$"\[{ProviderIds.ShizouEp}-(\d+)\]") is { Success: true } reg ? reg.Groups[1].Value : null;
        if (!int.TryParse(epIdStr, out var epId))
            return new MetadataResult<Episode>();

        var episode = await _plugin.ShizouHttpClient.AniDbEpisodesAsync(epId, cancellationToken).ConfigureAwait(false);


        var res = new MetadataResult<Episode>()
        {
            HasMetadata = true,
            Item = new Episode()
            {
                Name = episode.TitleEnglish,
                Overview = episode.Summary,
                RunTimeTicks = episode.DurationMinutes is not null ? TimeSpan.FromMinutes(episode.DurationMinutes.Value).Ticks : null,
                OriginalTitle = episode.TitleOriginal,
                PremiereDate = episode.AirDate?.UtcDateTime,
                ProductionYear = episode.AirDate?.Year,
                IndexNumber = episode.Number,
                // TODO: Set based on episodes associated with file
                IndexNumberEnd = episode.Number,
                ParentIndexNumber = episode.EpisodeType is AniDbEpisodeEpisodeType.Episode ? 1 : 0,
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.ShizouEp, epIdStr } }
            }
        };

        return res;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _plugin.HttpClient.GetAsync(url, cancellationToken);

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
