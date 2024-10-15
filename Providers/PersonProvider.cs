using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Shizou.JellyfinPlugin.Providers;

public class PersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
{
    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken) =>
        Task.FromResult<IEnumerable<RemoteSearchResult>>([]);

    public Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
    {
        if (info.GetProviderId(ProviderIds.ShizouCreator) is not { Length: > 0 } creatorId)
            return Task.FromResult(new MetadataResult<Person>());

        return Task.FromResult(new MetadataResult<Person>()
        {
            HasMetadata = true,
            Item = new Person()
            {
                Name = info.Name,
                ProviderIds = new Dictionary<string, string>() { { ProviderIds.ShizouCreator, creatorId } }
            }
        });
    }

    public string Name => "Shizou";

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
