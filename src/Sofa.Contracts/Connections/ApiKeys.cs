using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Connections;

public sealed record ApiKeyToSave(Vendor Vendor, string ApiKey);

public sealed record ApiKeySaved(Guid ApiKeyId);

public sealed record ConnectionRepository(string FullName, bool Private);
