using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Shizou.HttpClient;
using Shizou.JellyfinPlugin.Configuration;

namespace Shizou.JellyfinPlugin;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    private readonly ILogger<Plugin> _logger;
    private readonly SocketsHttpHandler _httpHandler;

    private readonly SemaphoreSlim _loggingInLock = new(1, 1);
    private bool _loggedIn;

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILogger<Plugin> logger) : base(applicationPaths, xmlSerializer)
    {
        _logger = logger;
        _httpHandler = new SocketsHttpHandler()
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };

        HttpClient = new System.Net.Http.HttpClient(_httpHandler);
        HttpClient.BaseAddress = new Uri("https://localhost");
        ShizouHttpClient = new ShizouHttpClient(HttpClient);
        Instance = this;
    }

    public static Plugin Instance { get; private set; } = null!;

    public override string Name => "Shizou";

    public override Guid Id => Guid.Parse("1E81A180-292D-4523-9D57-D03F5221C2F2");

    public System.Net.Http.HttpClient HttpClient { get; private set; }
    public ShizouHttpClient ShizouHttpClient { get; private set; }

    public async Task LoginAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _loggingInLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                await _loggingInLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!_loggedIn)
            {
                _logger.LogInformation("Logging in...");
                await ShizouHttpClient.AccountLoginAsync(Configuration.ServerPassword, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Successfully logged in");
            }

            _loggedIn = true;
        }
        finally
        {
            _loggingInLock.Release();
        }
    }

    public async Task Unauthorized(CancellationToken cancellationToken)
    {
        try
        {
            if (!await _loggingInLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                await _loggingInLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            _logger.LogWarning("Unauthorized, Logged Out!");
            _loggedIn = false;
        }
        finally
        {
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

    public IEnumerable<PluginPageInfo> GetPages() =>
    [
        new()
        {
            Name = Name,
            EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
        }
    ];
}
