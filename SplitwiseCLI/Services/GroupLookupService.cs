using SplitwiseCLI.Api;
using SplitwiseCLI.Models;

namespace SplitwiseCLI.Services;

public sealed record GroupLookup(IReadOnlyList<Group> Groups, IReadOnlyDictionary<string, Group> GroupsByName);

public sealed class GroupLookupService(ISplitwiseClient client)
{
    public async Task<GroupLookup> LoadAsync(CancellationToken cancellationToken = default)
    {
        var groups = await client.GetGroupsAsync(cancellationToken);

        var byName = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups)
        {
            // Group id 0 is Splitwise's pseudo-group for "non-group expenses" - not
            // a real group that expenses can be equally split among.
            if (group.Id == 0 || string.IsNullOrWhiteSpace(group.Name))
            {
                continue;
            }

            byName[group.Name] = group;
        }

        return new GroupLookup(groups, byName);
    }
}
