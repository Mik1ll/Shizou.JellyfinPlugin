using MediaBrowser.Model.Plugins;

namespace Shizou.JellyfinPlugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    private string _serverBaseAddress = "http://localhost";

    public string ServerBaseAddress
    {
        get => _serverBaseAddress;
        set
        {
            Plugin.Instance.NewHttpClient(new Uri(value));
            _serverBaseAddress = value;
        }
    }

    public string ServerPassword { get; set; } = string.Empty;
}
