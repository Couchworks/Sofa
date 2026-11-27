using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Identity.Data;

public sealed class IdentityDbAtDesignTime : IDesignTimeDbContextFactory<IdentityDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public IdentityDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDb>();
        IdentityDb.Configure(options, OnlyForGeneratingMigrations);
        return new IdentityDb(options.Options);
    }
}
