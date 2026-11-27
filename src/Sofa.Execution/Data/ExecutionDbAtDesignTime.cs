using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Execution.Data;

public sealed class ExecutionDbAtDesignTime : IDesignTimeDbContextFactory<ExecutionDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public ExecutionDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ExecutionDb>();
        ExecutionDb.Configure(options, OnlyForGeneratingMigrations);
        return new ExecutionDb(options.Options);
    }
}
