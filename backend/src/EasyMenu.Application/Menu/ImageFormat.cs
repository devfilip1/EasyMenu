namespace EasyMenu.Application.Menu;

/// <summary>Accepted photo formats, detected by magic bytes (research R4), never by the Content-Type header.</summary>
public sealed record ImageFormat(string Extension, string ContentType)
{
    public static readonly ImageFormat Jpeg = new("jpg", "image/jpeg");
    public static readonly ImageFormat Png = new("png", "image/png");
    public static readonly ImageFormat Webp = new("webp", "image/webp");

    /// <summary>Bytes needed to recognise every accepted format.</summary>
    public const int HeaderLength = 12;

    private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];
    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Returns the format of the file whose first bytes are <paramref name="header"/>, or null if not accepted.</summary>
    public static ImageFormat? Detect(ReadOnlySpan<byte> header)
    {
        if (header.StartsWith(JpegSignature))
        {
            return Jpeg;
        }

        if (header.StartsWith(PngSignature))
        {
            return Png;
        }

        // RIFF <4-byte size> WEBP
        if (header.Length >= HeaderLength && header.StartsWith("RIFF"u8) && header.Slice(8, 4).SequenceEqual("WEBP"u8))
        {
            return Webp;
        }

        return null;
    }
}
