using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Shizou.JellyfinPlugin.Extensions;

namespace Shizou.JellyfinPlugin.Services;

public class PlayedStateService
{
    private readonly ILogger<PlayedStateService> _logger;
    private readonly IUserManager _usermanager;
    private readonly IUserDataManager _userDataManager;
    private readonly ILibraryManager _libraryManager;

    public PlayedStateService(ILogger<PlayedStateService> logger, IUserManager usermanager, IUserDataManager userDataManager, ILibraryManager libraryManager)
    {
        _logger = logger;
        _usermanager = usermanager;
        _userDataManager = userDataManager;
        _libraryManager = libraryManager;
    }

    public async Task UpdateStates(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting watched state sync");
        var fileStates = (await Plugin.Instance.ShizouHttpClient.WithLoginRetry(
                (sc, ct) => sc.FileWatchedStatesGetAllAsync(ct), cancellationToken).ConfigureAwait(false))
            .ToDictionary(fs => fs.AniDbFileId);
        var adminUser = _usermanager.Users.First(u => u.HasPermission(PermissionKind.IsAdministrator));

        var videos = _libraryManager.GetItemList(new InternalItemsQuery(adminUser)
        {
            MediaTypes = [MediaType.Video],
            Recursive = true,
            IsFolder = false,
            SourceTypes = [SourceType.Library],
            IsVirtualItem = false,
            HasAnyProviderId = new Dictionary<string, string>() { { ProviderIds.ShizouEp, string.Empty } }
        });
        foreach (var vid in videos)
        {
            if (!fileStates.TryGetValue(Convert.ToInt32(vid.ProviderIds[ProviderIds.ShizouEp]), out var fileState))
                continue;
            var userDataItem = _userDataManager.GetUserData(adminUser, vid);
            if (userDataItem.Played != fileState.Watched)
            {
                _logger.LogInformation("Found out of sync played state: AniDB file ID: {AniDbFileId}, Jellyfin: {JellyState}, Shizou: {ShizouState}",
                    fileState.AniDbFileId, userDataItem.Played, fileState.Watched);

                _logger.LogInformation("Setting played state of item: {PlayedState} => {NewPlayedState}", userDataItem.Played, fileState.Watched);
                userDataItem.Played = fileState.Watched;
                _userDataManager.SaveUserData(adminUser, vid, userDataItem, UserDataSaveReason.TogglePlayed, cancellationToken);
            }
        }
    }
}
