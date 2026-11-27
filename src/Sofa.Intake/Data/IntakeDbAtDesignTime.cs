using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Intake.Data;

public sealed class IntakeDbAtDesignTime : IDesignTimeDbContextFactory<IntakeDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public IntakeDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IntakeDb>();
        IntakeDb.Configure(options, OnlyForGeneratingMigrations);
        return new IntakeDb(options.Options);
    }
}
