using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Code.Data;

public sealed class CodeDbAtDesignTime : IDesignTimeDbContextFactory<CodeDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public CodeDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CodeDb>();
        CodeDb.Configure(options, OnlyForGeneratingMigrations);
        return new CodeDb(options.Options);
    }
}
