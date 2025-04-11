using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Tasks;

namespace Shizou.JellyfinPlugin.Tasks;

public class GetMissingPeopleImages : IScheduledTask, IConfigurableScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IDirectoryService _directoryService;

    public GetMissingPeopleImages(ILibraryManager libraryManager, IDirectoryService directoryService)
    {
        _libraryManager = libraryManager;
        _directoryService = directoryService;
    }

    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var people = _libraryManager.GetPeopleItems(new InternalPeopleQuery());
        var itemsWithoutImage = people.Where(p => p.ImageInfos.Length == 0).ToList();
        foreach (var item in itemsWithoutImage)
            item.RefreshMetadata(
                new MetadataRefreshOptions(_directoryService)
                {
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                }, cancellationToken);
        return Task.CompletedTask;
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    public string Name => "Get Missing People Images";
    public string Key => nameof(GetMissingPeopleImages);
    public string Description => "Force refresh people's missing images";
    public string Category => "Shizou";
    public bool IsHidden => false;
    public bool IsEnabled => true;
    public bool IsLogged => true;
}
