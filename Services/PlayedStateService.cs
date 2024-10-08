using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;
using Shizou.JellyfinPlugin.Extensions;

namespace Shizou.JellyfinPlugin.Services;

public class PlayedStateService : IDisposable
{
    private static readonly SemaphoreSlim ThrottleConcurrentConnections = new(10, 10);
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
        _userDataManager.UserDataSaved += OnUserDataSaved;
    }

    public async Task UpdateStates(CancellationToken cancellationToken, IProgress<double>? progress = null)
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
        for (var idx = 0; idx < videos.Count; idx++)
        {
            var vid = videos[idx];
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

            progress?.Report((idx + 1.0) / videos.Count);
        }
    }

    public void Dispose()
    {
        _userDataManager.UserDataSaved -= OnUserDataSaved;
    }

    private async void OnUserDataSaved(object? sender, UserDataSaveEventArgs e)
    {
        if (e.SaveReason != UserDataSaveReason.TogglePlayed ||
            e.Item is not Video { MediaType: MediaType.Video, IsFolder: false, SourceType: SourceType.Library } vid ||
            !vid.ProviderIds.ContainsKey(ProviderIds.ShizouEp)) return;
        var adminUser = _usermanager.Users.FirstOrDefault(u => u.HasPermission(PermissionKind.IsAdministrator));

        if (adminUser is null || e.UserId != adminUser.Id || !int.TryParse(vid.ProviderIds[ProviderIds.ShizouEp], out var aniDbFileId))
            return;
        _logger.LogInformation("Updating Shizou watched state for AniDB file ID: {AniDbFileId}, {NewPlayedState}", aniDbFileId, e.UserData.Played);
        await ThrottleConcurrentConnections.WaitAsync().ConfigureAwait(false);
        try
        {
            await Plugin.Instance.ShizouHttpClient.WithLoginRetry(
                (cl, ct) => e.UserData.Played ? cl.AniDbFilesMarkWatchedAsync(aniDbFileId, ct) : cl.AniDbFilesMarkUnwatchedAsync(aniDbFileId, ct),
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) // Throws fatal error if not caught
        {
            _logger.LogError("Failed to update played state for AniDB file ID: {AniDbFileId}, {NewPlayedState}. Error: {ErrorMsg}", aniDbFileId,
                e.UserData.Played, ex.Message);
        }
        finally
        {
            ThrottleConcurrentConnections.Release();
        }
    }
}
