using Microsoft.Extensions.Options;
using Sofa.Records.Files;

namespace Sofa.UnitTests.Records;

public sealed class LocalDiskFilesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"sofa-files-{Guid.NewGuid():N}");

    private LocalDiskFiles Files => new(Options.Create(new FileStoreOptions { Root = _root }));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_file_saved_under_a_key_is_read_back_and_deleted()
    {
        var key = FileKeys.OfAttachment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await Files.SaveAsync(key, new MemoryStream([1, 2, 3]), Cancellation);
        await using (var read = await Files.OpenAsync(key, Cancellation))
        {
            using var copy = new MemoryStream();
            await read!.CopyToAsync(copy, Cancellation);
            Assert.Equal([1, 2, 3], copy.ToArray());
        }

        await Files.DeleteAsync(key, Cancellation);
        Assert.Null(await Files.OpenAsync(key, Cancellation));
        Assert.Empty(Directory.GetFiles(_root, "*.partial", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("workspaces/../../outside")]
    [InlineData("/etc/passwd")]
    [InlineData("a//b")]
    [InlineData("")]
    public async Task A_key_leading_out_of_the_root_is_refused(string key)
    {
        Assert.False(FileKeys.IsSafe(key));
        await Assert.ThrowsAsync<ArgumentException>(() => Files.SaveAsync(key, new MemoryStream([1]), Cancellation));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
