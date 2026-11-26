using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Menu;
using Microsoft.Extensions.Options;

namespace EasyMenu.Infrastructure.Storage;

/// <summary>Stores photos on the local disk under <see cref="StorageOptions.ImagesPath"/> (research R4).</summary>
public sealed class LocalDiskImageStorage(IOptions<StorageOptions> options) : IImageStorage
{
    private string Root => Path.GetFullPath(options.Value.ImagesPath);

    public long MaxImageBytes => options.Value.MaxImageBytes;

    public async Task<string> SaveAsync(Guid establishmentId, Stream content, ImageFormat format, CancellationToken cancellationToken)
    {
        var relativePath = $"{establishmentId}/{Guid.NewGuid():N}.{format.Extension}";
        var fullPath = FullPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            await using var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(file, cancellationToken);
        }
        catch
        {
            File.Delete(fullPath);
            throw;
        }

        return relativePath;
    }

    public void Delete(string relativePath) => File.Delete(FullPath(relativePath));

    // Never touch anything outside the storage root.
    private string FullPath(string relativePath)
    {
        var root = Root;
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        return fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? fullPath
            : throw new InvalidOperationException("Image path escapes the storage root.");
    }
}
