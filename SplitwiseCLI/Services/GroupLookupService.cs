using SplitwiseCLI.Api;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed record GroupLookup(IReadOnlyList<Group> Groups, IReadOnlyDictionary<long, Group> GroupsById);

public sealed class GroupLookupService(ISplitwiseClient client)
{
    public async Task<GroupLookup> LoadAsync(CancellationToken cancellationToken = default)
    {
        var groups = await client.GetGroupsAsync(cancellationToken);

        var byId = new Dictionary<long, Group>();
        foreach (var group in groups)
        {
            // Group id 0 is Splitwise's pseudo-group for "non-group expenses" - not
            // a real group that expenses can be equally split among.
            if (group.Id == 0)
            {
                continue;
            }

            byId[group.Id] = group;
        }

        return new GroupLookup(groups, byId);
    }
}
