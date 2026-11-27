using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Records.Data;

public sealed class RecordsDbAtDesignTime : IDesignTimeDbContextFactory<RecordsDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public RecordsDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RecordsDb>();
        RecordsDb.Configure(options, OnlyForGeneratingMigrations);
        return new RecordsDb(options.Options);
    }
}
