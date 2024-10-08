using MediaBrowser.Model.Plugins;

namespace Shizou.JellyfinPlugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ServerBaseAddress { get; set; } = "http://localhost";

    public string ServerPassword { get; set; } = string.Empty;
}
