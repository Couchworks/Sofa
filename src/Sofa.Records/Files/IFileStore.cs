namespace Sofa.Records.Files;

public interface IFileStore
{
    Task SaveAsync(string key, Stream content, CancellationToken cancellation);

    Task<Stream?> OpenAsync(string key, CancellationToken cancellation);

    Task DeleteAsync(string key, CancellationToken cancellation);
}
