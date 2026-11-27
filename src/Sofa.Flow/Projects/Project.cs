using Sofa.Contracts.Flow;
using Sofa.Contracts.Identity;

namespace Sofa.Flow.Projects;

public sealed class Project
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string Key { get; init; }

    public required string Name { get; set; }

    public required Guid RequestConnectionId { get; set; }

    public required string RequestRepository { get; set; }

    public required string RequestLabel { get; set; }

    public bool KeepsIssueState { get; set; }

    public required Guid CodeConnectionId { get; set; }

    public required List<string> CodeRepositories { get; set; }

    public long BindingVersion { get; set; } = 1;

    public int LastWorkItemNumber { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public RequestProviderBinding Requests => new(RequestConnectionId, RequestRepository, RequestLabel, KeepsIssueState);

    public CodeProviderBinding Code => new(CodeConnectionId, CodeRepositories);

    public ProjectBound Bound => new(Id, WorkspaceId, Key, Requests, Code, BindingVersion);

    public void BindTo(RequestProviderBinding requests, CodeProviderBinding code)
    {
        RequestConnectionId = requests.ConnectionId;
        RequestRepository = requests.Repository;
        RequestLabel = requests.Label;
        KeepsIssueState = requests.KeepsIssueState;
        CodeConnectionId = code.ConnectionId;
        CodeRepositories = [.. code.Repositories];
        BindingVersion++;
    }
}

public sealed class Phase
{
    public required Guid ProjectId { get; init; }

    public required PhaseName Name { get; init; }

    public required int Position { get; init; }

    public Pattern Pattern { get; init; } = Pattern.Solo;

    public required Guid CastAgentId { get; set; }

    public required Gate Gate { get; set; }

    public required Sandbox Sandbox { get; set; }
}

public sealed class Agent
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string Name { get; init; }

    public required Guid OwnerTeamId { get; init; }

    public required AgentRole Role { get; set; }

    public required string Instructions { get; set; }

    public required Vendor Vendor { get; set; }

    public required string Model { get; set; }

    public AgentRegistrationState Registration { get; set; } = AgentRegistrationState.Pending;

    public string? RegistrationRefusedBecause { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public void HeardFromIdentity(AgentRegistration registration)
    {
        Registration = registration.Registered ? AgentRegistrationState.Registered : AgentRegistrationState.Refused;
        RegistrationRefusedBecause = registration.RefusedBecause;
    }

    public void AskIdentityAgain()
    {
        Registration = AgentRegistrationState.Pending;
        RegistrationRefusedBecause = null;
    }
}
