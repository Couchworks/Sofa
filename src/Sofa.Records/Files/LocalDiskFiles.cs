using Microsoft.Extensions.Options;

namespace Sofa.Records.Files;

public sealed class FileStoreOptions
{
    public const string Section = "Records:Files";

    public string Root { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Sofa", "records");
}

public sealed class LocalDiskFiles(IOptions<FileStoreOptions> options) : IFileStore
{
    private string Root => Path.GetFullPath(options.Value.Root);

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellation)
    {
        var path = PathOf(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var beingWritten = $"{path}.{Guid.NewGuid():N}.partial";
        try
        {
            await using (var file = new FileStream(beingWritten, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await content.CopyToAsync(file, cancellation);
            }

            File.Move(beingWritten, path, overwrite: true);
        }
        finally
        {
            File.Delete(beingWritten);
        }
    }

    public Task<Stream?> OpenAsync(string key, CancellationToken cancellation)
    {
        var path = PathOf(key);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null);
    }

    public Task DeleteAsync(string key, CancellationToken cancellation)
    {
        File.Delete(PathOf(key));
        return Task.CompletedTask;
    }

    private string PathOf(string key)
    {
        if (!FileKeys.IsSafe(key))
        {
            throw new ArgumentException($"'{key}' is no key a file is kept under.", nameof(key));
        }

        var root = Root;
        var path = Path.GetFullPath(Path.Combine(root, key));
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? path
            : throw new ArgumentException($"'{key}' leads out of the files' root.", nameof(key));
    }
}
