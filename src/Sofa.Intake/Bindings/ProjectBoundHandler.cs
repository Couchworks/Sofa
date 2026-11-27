using Sofa.Contracts.Flow;
using Sofa.Intake.Data;

namespace Sofa.Intake.Bindings;

public static class ProjectBoundHandler
{
    public static async Task Handle(ProjectBound bound, IntakeDb db, ILogger<ProjectBound> logger, CancellationToken cancellation)
    {
        var kept = await db.RequestBindings.FindAsync([bound.ProjectId], cancellation);
        if (!BindingRules.IsNewerThanKept(kept, bound))
        {
            return;
        }

        var binding = BindingRules.Keep(kept, bound);
        if (kept is null)
        {
            db.RequestBindings.Add(binding);
        }

        await db.SaveChangesAsync(cancellation);
        if (binding.Removed)
        {
            logger.LogInformation("Project {Key} takes no more Requests (binding version {Version})", binding.ProjectKey, binding.Version);
            return;
        }

        logger.LogInformation("Project {Key} takes Requests from {Repository} labelled {Label} (binding version {Version})",
            binding.ProjectKey, binding.Repository, binding.Label, binding.Version);
    }
}
