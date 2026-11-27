using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Flow.Data;

public sealed class FlowDbAtDesignTime : IDesignTimeDbContextFactory<FlowDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public FlowDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FlowDb>();
        FlowDb.Configure(options, OnlyForGeneratingMigrations);
        return new FlowDb(options.Options);
    }
}
