using Sofa.Contracts.Flow;

namespace Sofa.Code.Bindings;

public static class BindingRules
{
    public static bool IsNewerThanKept(CodeBinding? kept, ProjectBound bound) => kept is null || bound.Version > kept.Version;

    public static CodeBinding Keep(CodeBinding? kept, ProjectBound bound)
    {
        var binding = kept ?? new CodeBinding { ProjectId = bound.ProjectId, WorkspaceId = bound.WorkspaceId, ProjectKey = bound.Key };
        binding.Version = bound.Version;
        binding.Removed = bound.Code is null;
        if (bound.Code is { } code)
        {
            binding.ConnectionId = code.ConnectionId;
            binding.Repositories = [.. code.Repositories];
        }

        return binding;
    }

    public static bool Holds(CodeBinding binding, Guid workspaceId, Guid connectionId, string repository) =>
        !binding.Removed
        && binding.WorkspaceId == workspaceId
        && binding.ConnectionId == connectionId
        && binding.Repositories.Contains(repository, StringComparer.OrdinalIgnoreCase);
}
