using System.Collections.Concurrent;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Caching.Memory;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private static readonly MemoryCache EpisodeCache = new(new MemoryCacheOptions());
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> CacheLocks = new();
    private readonly ShizouClientManager _shizouClientManager;

    public EpisodeProvider(ShizouClientManager shizouClientManager) => _shizouClientManager = shizouClientManager;

    public string Name => "Shizou";

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var fileId = Convert.ToInt32(info.GetProviderId(ProviderIds.ShizouEp) ?? AniDbIdParser.IdFromString(Path.GetFileName(info.Path)));
        var animeId = Convert.ToInt32(info.SeriesProviderIds.GetValueOrDefault(ProviderIds.Shizou));
        if (fileId == 0 || animeId == 0)
            return new MetadataResult<Episode>();

        List<AniDbEpisode>? episodes;
        var animeLock = CacheLocks.GetOrAdd(animeId.ToString(), _ => new SemaphoreSlim(1, 1));
        await animeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            episodes = (await EpisodeCache.GetOrCreateAsync(animeId.ToString(), async entry =>
            {
                entry.SlidingExpiration = TimeSpan.FromSeconds(10);
                var eps = await _shizouClientManager.WithLoginRetry(
                    sc => sc.AniDbEpisodesByAniDbAnimeIdAsync(animeId, cancellationToken), cancellationToken).ConfigureAwait(false);
                var xrefs = await _shizouClientManager.WithLoginRetry(
                    sc => sc.AniDbEpisodeFileXrefsByAniDbAnimeIdAsync(animeId, cancellationToken), cancellationToken).ConfigureAwait(false);
                return eps.Select(ep => new
                {
                    ep,
                    xrefs = xrefs.Where(xr => xr.AniDbEpisodeId == ep.Id).ToList(),
                }).ToList();
            }).ConfigureAwait(false))?.Where(e => e.xrefs.Any(xr => xr.AniDbFileId == fileId)).Select(e => e.ep).ToList();
        }
        finally
        {
            animeLock.Release();
        }

        if (episodes?.Count is null or 0)
            return new MetadataResult<Episode>();

        episodes = episodes.OrderBy(ep => ep.EpisodeType).ThenBy(ep => ep.Number).ToList();
        var episode = episodes.First();

        var lastNum = episode.Number;
        foreach (var ep in episodes.Where(ep => ep.EpisodeType == episode.EpisodeType && ep.Number != episode.Number)
                     .Select(ep => ep.Number).Distinct().Order())
            if (lastNum + 1 == ep)
                lastNum++;
            else
                break;

        DateTimeOffset? airDateOffset = episode.AirDate is null ? null : new DateTimeOffset(episode.AirDate.Value.DateTime, TimeSpan.FromHours(9));
        var result = new MetadataResult<Episode>
        {
            HasMetadata = true,
            Item = new Episode
            {
                Name = episode.EpisodeType switch
                    {
                        EpisodeType.Special => "S",
                        EpisodeType.Credits => "C",
                        EpisodeType.Trailer => "T",
                        EpisodeType.Parody => "P",
                        EpisodeType.Other => "O",
                        _ => "",
                    } + $"{episode.Number + (lastNum != episode.Number ? $"-{lastNum}" : "")}. {episode.TitleEnglish}",
                Overview = episode.Summary,
                RunTimeTicks = episode.DurationMinutes is not null ? TimeSpan.FromMinutes(episode.DurationMinutes.Value).Ticks : null,
                OriginalTitle = episode.TitleOriginal,
                PremiereDate = airDateOffset?.UtcDateTime,
                ProductionYear = airDateOffset?.Year,
                IndexNumber = (int)episode.EpisodeType * 1000 + episode.Number,
                IndexNumberEnd = lastNum != episode.Number ? (int)episode.EpisodeType * 1000 + lastNum : null,
                ParentIndexNumber = episode.EpisodeType == EpisodeType.Episode ? null : 0,
                ProviderIds = new Dictionary<string, string> { { ProviderIds.ShizouEp, fileId.ToString() } },
            },
        };

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
