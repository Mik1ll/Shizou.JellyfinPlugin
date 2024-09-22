using MediaBrowser.Model.Tasks;
using Shizou.JellyfinPlugin.Services;

namespace Shizou.JellyfinPlugin.Tasks;

public class SyncPlayedTask : IScheduledTask, IConfigurableScheduledTask
{
    private readonly PlayedStateService _playedStateService;

    public SyncPlayedTask(PlayedStateService playedStateService) => _playedStateService = playedStateService;

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken) => _playedStateService.UpdateStates(cancellationToken);

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public string Name => "Sync Played Status";
    public string Key => "SyncPlayedTask";
    public string Description => "Sync the watched states in Shizou with the played states in Jellyfin";
    public string Category => "Shizou";
    public bool IsHidden => false;
    public bool IsEnabled => true;
    public bool IsLogged => true;
}
