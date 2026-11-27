using Sofa.Contracts.Flow;

namespace Sofa.Intake.Bindings;

public static class BindingRules
{
    public static bool IsNewerThanKept(RequestBinding? kept, ProjectBound bound) => kept is null || bound.Version > kept.Version;

    public static RequestBinding Keep(RequestBinding? kept, ProjectBound bound)
    {
        var binding = kept ?? new RequestBinding { ProjectId = bound.ProjectId, WorkspaceId = bound.WorkspaceId, ProjectKey = bound.Key };
        binding.Version = bound.Version;
        binding.Removed = bound.Requests is null;
        if (bound.Requests is { } requests)
        {
            binding.ConnectionId = requests.ConnectionId;
            binding.Repository = requests.Repository;
            binding.Label = requests.Label;
            binding.KeepsIssueState = requests.KeepsIssueState;
        }

        return binding;
    }

    public static bool Marks(RequestBinding binding, Guid workspaceId, Guid connectionId, string repository, string label) =>
        !binding.Removed
        && binding.WorkspaceId == workspaceId
        && binding.ConnectionId == connectionId
        && binding.Repository.Equals(repository, StringComparison.OrdinalIgnoreCase)
        && binding.Label.Equals(label.Trim(), StringComparison.OrdinalIgnoreCase);
}
