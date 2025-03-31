using MediaBrowser.Model.Plugins;
// ReSharper disable UnusedMember.Global
// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

namespace Shizou.JellyfinPlugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    private string _serverBaseAddress = "http://localhost";

    public string ServerBaseAddress
    {
        get => _serverBaseAddress;
        set
        {
            ShizouClientManager.Instance?.SetBaseUrl(value);
            _serverBaseAddress = value;
        }
    }

    public string ServerPassword { get; set; } = string.Empty;
}
