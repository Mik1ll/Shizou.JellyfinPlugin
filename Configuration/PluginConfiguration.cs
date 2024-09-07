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
            if (Plugin.Instance is null)
                throw new InvalidOperationException("Plugin instance is null");
            Plugin.Instance.ChangeHttpClientPort(value);
            _serverPort = value;
        }
    }

    public string ServerPassword { get; set; } = string.Empty;
}
