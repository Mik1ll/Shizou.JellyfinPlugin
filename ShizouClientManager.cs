using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shizou.HttpClient;

namespace Shizou.JellyfinPlugin;

public class ShizouClientManager
{
    private readonly ShizouHttpClient _shizouHttpClient;
    private readonly System.Net.Http.HttpClient _httpClient;
    private readonly ILogger<ShizouClientManager> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly SemaphoreSlim _loggingInLock = new(1, 1);

    private DateTimeOffset? _lastLogin;

    public ShizouClientManager(ILogger<ShizouClientManager> logger, IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _httpClient = new System.Net.Http.HttpClient();
        _shizouHttpClient = new ShizouHttpClient(Plugin.Instance.Configuration.ServerBaseAddress, _httpClient);
        Instance = this;
    }

    public static ShizouClientManager? Instance { get; private set; }

    private static async Task<T?> Catch404<T>(Task<T> task)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
        {
            return default;
        }
    }

    public async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken)
    {
        var uri = new Uri(new Uri(_shizouHttpClient.BaseUrl), url).AbsoluteUri;
        var res = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        if (res.StatusCode != HttpStatusCode.Unauthorized)
            return res;

        await LoginAsync(cancellationToken).ConfigureAwait(false);
        res = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);

        return res;
    }

    public Task<ICollection<FileWatchedState>> GetAllWatchedStates(CancellationToken cancellationToken) =>
        WithLoginRetry(ct => _shizouHttpClient.FileWatchedStatesGetAllAsync(ct), cancellationToken);

    public Task MarkFilePlayedState(int fileId, bool played, CancellationToken cancellationToken) =>
        WithLoginRetry(ct => played
            ? _shizouHttpClient.AniDbFilesMarkWatchedAsync(fileId, ct)
            : _shizouHttpClient.AniDbFilesMarkUnwatchedAsync(fileId, ct), cancellationToken);

    public async Task<(byte[] img, string mimeType)?> GetCreatorImageAsync(int creatorId, CancellationToken cancellationToken)
    {
        var key = $"anidb-creator-image-{creatorId}";
        var res = await _memoryCache.GetOrCreateAsync<(byte[], string)?>(key, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            var img = await Catch404(WithLoginRetry(ct => _shizouHttpClient.ImagesGetCreatorImageAsync(creatorId, ct), cancellationToken))
                .ConfigureAwait(false);

            if (img is null)
                return null;

            await using (img.Stream.ConfigureAwait(false))
            {
                using var memStream = new MemoryStream();
                await img.Stream.CopyToAsync(memStream, cancellationToken).ConfigureAwait(false);
                return (memStream.ToArray(), img.Headers.GetValueOrDefault("Content-Type", ["image/jpeg"]).First());
            }
        }).ConfigureAwait(false);

        return res;
    }

    public async Task<AniDbAnime?> GetAnimeAsync(int animeId, CancellationToken cancellationToken)
    {
        var key = $"anidb-series-{animeId}";
        var anime = await GetCachedOrFallback(key, ct => _shizouHttpClient.AniDbAnimesGetAsync(animeId, ct), cancellationToken).ConfigureAwait(false);
        return anime;
    }

    public async Task<ICollection<AniDbCredit>?> GetCreditsAsync(int animeId, CancellationToken cancellationToken)
    {
        var key = $"anidb-credits-{animeId}";
        var credits = await GetCachedOrFallback(key,
            ct => _shizouHttpClient.AniDbCreditsByAniDbAnimeIdAsync(animeId, ct),
            cancellationToken).ConfigureAwait(false);
        return credits;
    }

    public async Task<ICollection<AniDbEpisode>?> GetEpisodesAsync(int animeId, CancellationToken cancellationToken)
    {
        var key = $"anidb-episodes-{animeId}";
        var episodes = await GetCachedOrFallback(key,
            ct => _shizouHttpClient.AniDbEpisodesByAniDbAnimeIdAsync(animeId, ct),
            cancellationToken).ConfigureAwait(false);

        return episodes;
    }

    public async Task<ICollection<AniDbEpisodeFileXref>?> GetEpFileXrefsAsync(int animeId, CancellationToken cancellationToken)
    {
        var key = $"anidb-epfilexrefs-{animeId}";
        var xrefs = await GetCachedOrFallback(key,
            ct => _shizouHttpClient.AniDbEpisodeFileXrefsByAniDbAnimeIdAsync(animeId, ct),
            cancellationToken).ConfigureAwait(false);
        return xrefs;
    }

    private async Task<T?> GetCachedOrFallback<T>(string key, Func<CancellationToken, Task<T>> getter, CancellationToken cancellationToken)
    {
        var result = await _memoryCache.GetOrCreateAsync(key, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(1);
            var r = await Catch404(WithLoginRetry(getter, cancellationToken)).ConfigureAwait(false);
            if (r is null)
                entry.Dispose();
            return r;
        }).ConfigureAwait(false);
        return result;
    }

    private async Task<T> WithLoginRetry<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await action(cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);
            return await action(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task WithLoginRetry(Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);
            await action(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _loggingInLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                await _loggingInLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogTrace("Obtained login lock after waiting, skipping login");
                return;
            }

            if (_lastLogin is not null && DateTimeOffset.Now < _lastLogin + TimeSpan.FromSeconds(10))
            {
                _logger.LogWarning("Logged in less than 10 seconds ago, not retrying");
                return;
            }

            _logger.LogInformation("Logging in...");
            await _shizouHttpClient.AccountLoginAsync(Plugin.Instance.Configuration.ServerPassword, cancellationToken).ConfigureAwait(false);
            _lastLogin = DateTimeOffset.Now;
            _logger.LogInformation("Successfully logged in");
        }
        finally
        {
            _loggingInLock.Release();
        }
    }

    public void SetBaseUrl(string baseUrl)
    {
        _shizouHttpClient.BaseUrl = baseUrl;
    }
}
