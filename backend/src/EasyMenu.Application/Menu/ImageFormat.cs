namespace EasyMenu.Application.Menu;

/// <summary>Accepted photo formats, detected by magic bytes (research R4), never by the Content-Type header.</summary>
/// <remarks>Stub created by the test author (T027) so the tests compile; implementation belongs to T029.</remarks>
public sealed record ImageFormat(string Extension, string ContentType)
{
    public static readonly ImageFormat Jpeg = new("jpg", "image/jpeg");
    public static readonly ImageFormat Png = new("png", "image/png");
    public static readonly ImageFormat Webp = new("webp", "image/webp");

    /// <summary>Returns the format of the file whose first bytes are <paramref name="header"/>, or null if not accepted.</summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header) => throw new NotImplementedException();
}
