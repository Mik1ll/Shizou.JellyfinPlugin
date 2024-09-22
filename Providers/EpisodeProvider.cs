using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.AspNetCore.Http;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.ExternalIds;

namespace Shizou.JellyfinPlugin.Providers;

public class EpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
{
    public string Name => "Shizou";

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var fileId = info.GetProviderId(ProviderIds.ShizouEp) ?? AniDbIdParser.IdFromString(Path.GetFileName(info.Path));
        if (string.IsNullOrWhiteSpace(fileId))
            return new MetadataResult<Episode>();

        var animeId = Convert.ToInt32(info.SeriesProviderIds.GetValueOrDefault(ProviderIds.Shizou));
        List<AniDbEpisode> episodes;

        await Plugin.Instance.LoginAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            episodes =
                (await Plugin.Instance.ShizouHttpClient.AniDbEpisodesByAniDbFileIdAsync(Convert.ToInt32(fileId), cancellationToken).ConfigureAwait(false))
                .ToList();
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await Plugin.Instance.Unauthorized(cancellationToken).ConfigureAwait(false);
            await Plugin.Instance.LoginAsync(cancellationToken).ConfigureAwait(false);
            episodes =
                (await Plugin.Instance.ShizouHttpClient.AniDbEpisodesByAniDbFileIdAsync(Convert.ToInt32(fileId), cancellationToken).ConfigureAwait(false))
                .ToList();
        }

        episodes = episodes.Where(ep => animeId == 0 || ep.AniDbAnimeId == animeId)
            .OrderBy(ep => ep.EpisodeType).ThenBy(ep => ep.Number).ToList();
        var episode = episodes.FirstOrDefault();

        if (episode is null)
            return new MetadataResult<Episode>();

        var lastNum = episode.Number;
        foreach (var ep in episodes.Where(ep => ep.EpisodeType == episode.EpisodeType && ep.Number != episode.Number)
                     .Select(ep => ep.Number).Distinct().Order())
            if (lastNum + 1 == ep)
                lastNum++;
            else
                break;

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
                    } + $"{episode.Number + (lastNum != episode.Number ? $"-{lastNum}" : "")}. {episode.TitleEnglish}",
                Overview = episode.Summary,
                RunTimeTicks = episode.DurationMinutes is not null ? TimeSpan.FromMinutes(episode.DurationMinutes.Value).Ticks : null,
                OriginalTitle = episode.TitleOriginal,
                PremiereDate = episode.AirDate?.UtcDateTime,
                ProductionYear = episode.AirDate?.Year,
                IndexNumber = (int)episode.EpisodeType * 1000 + episode.Number,
                IndexNumberEnd = lastNum != episode.Number ? (int)episode.EpisodeType * 1000 + lastNum : null,
                ParentIndexNumber = episode.EpisodeType == AniDbEpisodeEpisodeType.Episode ? null : 0,
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.ShizouEp, fileId } }
            }
        };

        return result;
    }

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        Plugin.Instance.HttpClient.GetAsync(url, cancellationToken);

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);
}
