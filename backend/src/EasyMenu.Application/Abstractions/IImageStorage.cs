using EasyMenu.Application.Menu;

namespace EasyMenu.Application.Abstractions;

/// <summary>Stores menu item photos (research R4). Paths are relative to the storage root and served under /media.</summary>
public interface IImageStorage
{
    /// <summary>Largest accepted photo, in bytes (<c>Storage:MaxImageBytes</c>).</summary>
    long MaxImageBytes { get; }

    /// <summary>Saves <paramref name="content"/> as <c>{establishmentId}/{new guid}.{ext}</c> and returns that relative path.</summary>
    Task<string> SaveAsync(Guid establishmentId, Stream content, ImageFormat format, CancellationToken cancellationToken);

    /// <summary>Deletes a previously saved file; missing files are ignored.</summary>
    void Delete(string relativePath);
}
