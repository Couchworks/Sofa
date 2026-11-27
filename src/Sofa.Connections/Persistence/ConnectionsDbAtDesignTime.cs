using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Sofa.Connections.Persistence;

public sealed class ConnectionsDbAtDesignTime : IDesignTimeDbContextFactory<ConnectionsDb>
{
    public ConnectionsDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConnectionsDb>();
        options.UseConnectionsDefaults("Host=only-used-to-generate-migrations");
        return new ConnectionsDb(options.Options);
    }
}
