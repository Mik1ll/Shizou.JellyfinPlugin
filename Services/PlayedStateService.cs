using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Logging;
using Shizou.JellyfinPlugin.Extensions;

namespace Shizou.JellyfinPlugin.Services;

public class PlayedStateService
{
    private readonly ILogger<PlayedStateService> _logger;
    private readonly IUserManager _usermanager;
    private readonly IUserDataManager _userDataManager;
    private readonly ISessionManager _sessionManager;

    public PlayedStateService(ILogger<PlayedStateService> logger, IUserManager usermanager, IUserDataManager userDataManager, ISessionManager sessionManager)
    {
        _logger = logger;
        _usermanager = usermanager;
        _userDataManager = userDataManager;
        _sessionManager = sessionManager;
        _logger.LogInformation("Played Service Created");
    }

    public async Task UpdateStates(CancellationToken cancellationToken)
    {
        var fileStates = await Plugin.Instance.ShizouHttpClient.WithLoginRetry(
            (sc, ct) => sc.FileWatchedStatesGetAllAsync(ct), cancellationToken).ConfigureAwait(false);
        var adminUser = _usermanager.Users.First(u => u.HasPermission(PermissionKind.IsAdministrator));
        var userData = _userDataManager.GetAllUserData(adminUser.Id);
        ;
    }
}
