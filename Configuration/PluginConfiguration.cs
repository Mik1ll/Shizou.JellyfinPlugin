using MediaBrowser.Model.Plugins;

namespace Shizou.JellyfinPlugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    private int _serverPort = 443;

    public int ServerPort
    {
        get => _serverPort;
        set
        {
            Plugin.Instance.ChangeHttpClientPort(value);
            _serverPort = value;
        }
    }

    public string ServerPassword { get; set; } = string.Empty;
}
