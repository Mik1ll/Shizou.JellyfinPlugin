using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Shizou.HttpClient;

namespace Shizou.JellyfinPlugin;

public class ShizouClientManager
{
    public readonly ShizouHttpClient ShizouHttpClient;
    public readonly System.Net.Http.HttpClient HttpClient;
    private readonly ILogger<ShizouClientManager> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly SemaphoreSlim _loggingInLock = new(1, 1);

    private bool _loggedIn = false;
    private DateTimeOffset? _lastLogin;

    public ShizouClientManager(ILogger<ShizouClientManager> logger, IMemoryCache memoryCache)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        HttpClient = new System.Net.Http.HttpClient();
        ShizouHttpClient = new ShizouHttpClient(Plugin.Instance.Configuration.ServerBaseAddress, HttpClient);
    }

    public async Task<T> WithLoginRetry<T>(Func<ShizouHttpClient, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action(ShizouHttpClient).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);
            return await action(ShizouHttpClient).ConfigureAwait(false);
        }
    }

    public async Task WithLoginRetry(Func<ShizouHttpClient, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(ShizouHttpClient).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);
            await action(ShizouHttpClient).ConfigureAwait(false);
        }
    }

    public async Task LoginAsync(CancellationToken cancellationToken)
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
            await ShizouHttpClient.AccountLoginAsync(Plugin.Instance.Configuration.ServerPassword, cancellationToken).ConfigureAwait(false);
            _lastLogin = DateTimeOffset.Now;
            _logger.LogInformation("Successfully logged in");
        }
        finally
        {
            _loggingInLock.Release();
        }
    }
}
