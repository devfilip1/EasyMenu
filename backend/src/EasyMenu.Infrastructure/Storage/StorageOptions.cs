namespace EasyMenu.Infrastructure.Storage;

/// <summary>Photo storage settings (section <c>Storage</c>).</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Root directory of the photos. The host resolves it to an absolute path (relative to the content root).</summary>
    public string ImagesPath { get; set; } = "data/uploads";

    public long MaxImageBytes { get; set; } = 5 * 1024 * 1024;
}
