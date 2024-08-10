using MediaBrowser.Model.Plugins;

namespace Shizou.JellyfinPlugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public int ServerPort { get; set; } = 443;
}
