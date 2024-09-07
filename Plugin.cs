using System.Globalization;
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
    public ResilienceHandler ResilienceHandler { get; private set; }
#pragma warning restore EXTEXP0001
    public static Plugin? Instance { get; private set; }

    public System.Net.Http.HttpClient HttpClient { get; private set; }
    public ShizouHttpClient ShizouHttpClient { get; private set; }

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
    {
        var retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddConcurrencyLimiter(2, int.MaxValue)
            // .AddRetry(new HttpRetryStrategyOptions()
            // {
            //     MaxRetryAttempts = 1,
            //     ShouldHandle = static args => ValueTask.FromResult(args is { Outcome.Result.StatusCode: HttpStatusCode.Unauthorized }),
            //     OnRetry = static async _ =>
            //     {
            //         if (Instance is null)
            //             throw new InvalidOperationException("Shizou Instance is null");
            //         if (Instance.ShizouHttpClient is null)
            //             throw new InvalidOperationException("Shizou Http Client is null");
            //         await Instance.ShizouHttpClient.LoginAsync(Instance.Configuration.ServerPassword).ConfigureAwait(false);
            //     }
            // })
            .Build();

#pragma warning disable EXTEXP0001
        ResilienceHandler = new ResilienceHandler(retryPipeline)
#pragma warning restore EXTEXP0001
        {
            InnerHandler = new SocketsHttpHandler()
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            }
        };
        HttpClient = new System.Net.Http.HttpClient(ResilienceHandler);
        HttpClient.BaseAddress = new Uri("https://localhost");
        ShizouHttpClient = new ShizouHttpClient(HttpClient);
        Instance = this;
    }

    public override string Name => "Shizou";

    public override Guid Id => Guid.Parse("1E81A180-292D-4523-9D57-D03F5221C2F2");

    public void ChangeHttpClientPort(int port)
    {
        if (port == HttpClient.BaseAddress?.Port)
            return;
        var uri = new UriBuilder(HttpClient.BaseAddress!)
        {
            Port = port
        }.Uri;
        HttpClient.Dispose();
        HttpClient = new System.Net.Http.HttpClient(ResilienceHandler);
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
