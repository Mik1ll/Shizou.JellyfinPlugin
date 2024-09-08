using System.Globalization;
using System.Net;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.Configuration;

namespace Shizou.JellyfinPlugin;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
#pragma warning disable EXTEXP0001
    private readonly ResilienceHandler _httpHandler;
#pragma warning restore EXTEXP0001

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
        var retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new HttpRetryStrategyOptions()
            {
                MaxRetryAttempts = 1,
                ShouldHandle = static args => ValueTask.FromResult(args is { Outcome.Result.StatusCode: HttpStatusCode.Unauthorized }),
                OnRetry = async _ =>
                {
                    await _loggingInLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        _loggedIn = false;
                        await LoginAsync(CancellationToken.None, true).ConfigureAwait(false);
                    }
                    finally
                    {
                        _loggingInLock.Release();
                    }
                }
            })
            .Build();
#pragma warning disable EXTEXP0001
        _httpHandler = new ResilienceHandler(retryPipeline)
#pragma warning restore EXTEXP0001
        {
            InnerHandler = new SocketsHttpHandler()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            }
        };


        HttpClient = new System.Net.Http.HttpClient(_httpHandler);
        HttpClient.BaseAddress = new Uri("https://localhost");
        ShizouHttpClient = new ShizouHttpClient(HttpClient);
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Shizou";

    public override Guid Id => Guid.Parse("1E81A180-292D-4523-9D57-D03F5221C2F2");

    public System.Net.Http.HttpClient HttpClient { get; private set; }
    public ShizouHttpClient ShizouHttpClient { get; private set; }

    private bool _loggedIn;

    private readonly SemaphoreSlim _loggingInLock = new(1, 1);

    public async Task LoginAsync(CancellationToken cancellationToken, bool insideLock = false)
    {
        if (!insideLock)
            await _loggingInLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_loggedIn)
                await ShizouHttpClient.LoginAsync(Configuration.ServerPassword, cancellationToken).ConfigureAwait(false);
            _loggedIn = true;
        }
        finally
        {
            if (!insideLock)
                _loggingInLock.Release();
        }
    }

    public void ChangeHttpClientPort(int port)
    {
        if (port == HttpClient.BaseAddress?.Port)
            return;
        var uri = new UriBuilder(HttpClient.BaseAddress!)
        {
            Port = port
        }.Uri;
        HttpClient.Dispose();
        HttpClient = new System.Net.Http.HttpClient(_httpHandler);
        HttpClient.BaseAddress = uri;
        ShizouHttpClient = new ShizouHttpClient(HttpClient);
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        };
    }
}
