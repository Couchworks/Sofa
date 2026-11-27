using Sofa.Code.Data;
using Sofa.Contracts.Flow;

namespace Sofa.Code.Bindings;

public static class ProjectBoundHandler
{
    public static async Task Handle(ProjectBound bound, CodeDb db, ILogger<ProjectBound> logger, CancellationToken cancellation) =>
        await KeepAsync(bound, db, logger, cancellation);

    public static async Task<CodeBinding?> KeepAsync(ProjectBound bound, CodeDb db, ILogger logger, CancellationToken cancellation)
    {
        var kept = await db.Bindings.FindAsync([bound.ProjectId], cancellation);
        if (!BindingRules.IsNewerThanKept(kept, bound))
        {
            return kept;
        }

        var binding = BindingRules.Keep(kept, bound);
        if (kept is null)
        {
            db.Bindings.Add(binding);
        }

        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Project {Key}'s code is in {Repositories} (binding version {Version})",
            binding.ProjectKey, binding.Removed ? "no Repositories" : string.Join(", ", binding.Repositories), binding.Version);
        return binding;
    }
}
