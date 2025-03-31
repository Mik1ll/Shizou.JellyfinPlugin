using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    private readonly ShizouClientManager _shizouClientManager;

    public EpisodeProvider(ShizouClientManager shizouClientManager) => _shizouClientManager = shizouClientManager;

    public string Name => "Shizou";

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var fileId = Convert.ToInt32(info.GetProviderId(ProviderIds.ShizouEp) ?? AniDbIdParser.IdFromString(Path.GetFileName(info.Path)));
        var animeId = Convert.ToInt32(info.SeriesProviderIds.GetValueOrDefault(ProviderIds.Shizou));
        if (fileId == 0 || animeId == 0)
            return new MetadataResult<Episode>();

        var epIds = (await _shizouClientManager.GetEpFileXrefsAsync(animeId, cancellationToken).ConfigureAwait(false))
            ?.Where(xr => xr.AniDbFileId == fileId).Select(xr => xr.AniDbEpisodeId).ToHashSet();
        if (epIds is null)
            return new MetadataResult<Episode>();
        var episodes = (await _shizouClientManager.GetEpisodesAsync(animeId, cancellationToken).ConfigureAwait(false))
            ?.Where(e => epIds.Contains(e.Id)).OrderBy(GetEpIndex).ToList();
        if (episodes?.Count is null or 0)
            return new MetadataResult<Episode>();

        var episode = episodes.First();

        var lastNum = episode.Number - 1;
        // ReSharper disable once AccessToModifiedClosure
        var lastEpisode = episodes.DistinctBy(ep => ep.Number)
            .Where(ep => ep.EpisodeType == episode.EpisodeType)
            .OrderBy(GetEpIndex)
            .TakeWhile(ep => ++lastNum == ep.Number)
            .Last();

        var epName = string.Join(" / ", episodes.Select(e => e.TitleEnglish));

        DateTimeOffset? airDateOffset = episode.AirDate is null ? null : new DateTimeOffset(episode.AirDate.Value.DateTime, TimeSpan.FromHours(9));

        var overview = episode.Summary is null ? null : SeriesProvider.AniDbLinksToMarkDown(episode.Summary);

        var result = new MetadataResult<Episode>
        {
            HasMetadata = true,
            Item = new Episode
            {
                Name = epName,
                Overview = overview,
                RunTimeTicks = episode.DurationMinutes is not null ? TimeSpan.FromMinutes(episode.DurationMinutes.Value).Ticks : null,
                OriginalTitle = episode.TitleOriginal,
                PremiereDate = airDateOffset?.UtcDateTime,
                ProductionYear = airDateOffset?.Year,
                IndexNumber = GetEpIndex(episode),
                IndexNumberEnd = lastEpisode != episode ? GetEpIndex(lastEpisode) : null,
                ParentIndexNumber = episode.EpisodeType == EpisodeType.Episode ? 1 : 0,
                ProviderIds = new Dictionary<string, string> { { ProviderIds.ShizouEp, fileId.ToString() } },
            },
        };

        return result;

        int GetEpIndex(AniDbEpisode ep) =>
            ep.EpisodeType switch
            {
                EpisodeType.Episode => 0,
                EpisodeType.Other => 1,
                EpisodeType.Special => 2,
                EpisodeType.Credits => 3,
                EpisodeType.Trailer => 4,
                EpisodeType.Parody => 5,
                _ => throw new IndexOutOfRangeException(nameof(ep.EpisodeType)),
            } * 10000 + ep.Number;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
