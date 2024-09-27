using Microsoft.AspNetCore.Http;
using Shizou.HttpClient;

namespace Shizou.JellyfinPlugin.Extensions;

public static class ShizouHttpClientExtensions
{
    public static async Task<T> WithLoginRetry<T>(this ShizouHttpClient client,
        Func<ShizouHttpClient, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action(client, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await Plugin.Instance.LoginAsync(cancellationToken).ConfigureAwait(false);
            return await action(client, cancellationToken).ConfigureAwait(false);
        }
    }

    public static async Task WithLoginRetry(this ShizouHttpClient client,
        Func<ShizouHttpClient, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await action(client, cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == StatusCodes.Status401Unauthorized)
        {
            await Plugin.Instance.LoginAsync(cancellationToken).ConfigureAwait(false);
            await action(client, cancellationToken).ConfigureAwait(false);
        }
    }
}
