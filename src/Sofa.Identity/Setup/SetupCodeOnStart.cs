using Microsoft.EntityFrameworkCore;
using Sofa.Identity.Data;

namespace Sofa.Identity.Setup;

public static partial class SetupCodeOnStart
{
    public static async Task IssueWhileNotSetUpAsync(IdentityDb db, ILogger logger, CancellationToken cancellation)
    {
        var installation = await db.Installations.SingleOrDefaultAsync(cancellation);
        if (installation is null)
        {
            installation = new Installation();
            db.Installations.Add(installation);
        }

        if (installation.IsSetUp)
        {
            return;
        }

        var code = SetupCodes.Issue();
        installation.SetupCodeHash = SetupCodes.Hash(code);
        await db.SaveChangesAsync(cancellation);

        LogSetupCode(logger, code);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Sofa is not set up yet. Open it and enter this setup code: {SetupCode}")]
    private static partial void LogSetupCode(ILogger logger, string setupCode);
}
