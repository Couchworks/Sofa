using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Notices.Data;

public sealed class NoticesDbAtDesignTime : IDesignTimeDbContextFactory<NoticesDb>
{
    private const string OnlyForGeneratingMigrations = "Host=localhost;Database=sofa";

    public NoticesDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NoticesDb>();
        NoticesDb.Configure(options, OnlyForGeneratingMigrations);
        return new NoticesDb(options.Options);
    }
}
