using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private readonly Plugin _plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is null");

    public string Name => "Shizou";

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var epId = info.GetProviderId(ProviderIds.ShizouEp) ?? AniDbIdParser.IdFromString(Path.GetFileName(info.Path));
        if (string.IsNullOrWhiteSpace(epId))
            return new MetadataResult<Episode>();

        var episode = await _plugin.ShizouHttpClient.AniDbEpisodesGetAsync(Convert.ToInt32(epId), cancellationToken).ConfigureAwait(false);

        var result = new MetadataResult<Episode>()
        {
            HasMetadata = true,
            Item = new Episode()
            {
                Name = episode.EpisodeType switch
                    {
                        AniDbEpisodeEpisodeType.Special => "S",
                        AniDbEpisodeEpisodeType.Credits => "C",
                        AniDbEpisodeEpisodeType.Trailer => "T",
                        AniDbEpisodeEpisodeType.Parody => "P",
                        AniDbEpisodeEpisodeType.Other => "O",
                        _ => ""
                    } + $"{episode.Number}. {episode.TitleEnglish}",
                Overview = episode.Summary,
                RunTimeTicks = episode.DurationMinutes is not null ? TimeSpan.FromMinutes(episode.DurationMinutes.Value).Ticks : null,
                OriginalTitle = episode.TitleOriginal,
                PremiereDate = episode.AirDate?.UtcDateTime,
                ProductionYear = episode.AirDate?.Year,
                IndexNumber = episode.Number,
                // TODO: Set based on episodes associated with file
                IndexNumberEnd = null,
                ParentIndexNumber = episode.EpisodeType is AniDbEpisodeEpisodeType.Episode ? null : 0,
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.ShizouEp, epId } }
            }
        };

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        _plugin.HttpClient.GetAsync(url, cancellationToken);

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
