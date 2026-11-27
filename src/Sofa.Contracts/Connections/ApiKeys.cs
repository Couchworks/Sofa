using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Connections;

public sealed record ApiKeyToSave(Vendor Vendor, string ApiKey);

public sealed record ApiKeySaved(Guid ApiKeyId);

public sealed record ApiKeyForARun(Guid RunId);

public sealed record OpenedApiKey(string ApiKey);

public sealed record ConnectionRepository(string FullName, bool Private);

public static class ApiKeysForRuns
{
    public const string ExecutionsSecretHeader = "Sofa-Execution-Secret";
}
