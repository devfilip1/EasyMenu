using System.Text;
using EasyMenu.Application.Menu;
using FluentAssertions;

namespace EasyMenu.UnitTests.Application.Menu;

public sealed class ImageFormatTests
{
    private static byte[] Bytes(params int[] values) => values.Select(v => (byte)v).ToArray();

    private static byte[] WithPadding(byte[] header, int extra = 32) => [.. header, .. new byte[extra]];

    [Fact]
    public void Detects_jpeg()
    {
        ImageFormat.Detect(WithPadding(Bytes(0xFF, 0xD8, 0xFF, 0xE0))).Should().Be(ImageFormat.Jpeg);
    }

    [Fact]
    public void Detects_jpeg_with_exif_marker()
    {
        ImageFormat.Detect(WithPadding(Bytes(0xFF, 0xD8, 0xFF, 0xE1))).Should().Be(ImageFormat.Jpeg);
    }

    [Fact]
    public void Detects_png()
    {
        ImageFormat.Detect(WithPadding(Bytes(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))).Should().Be(ImageFormat.Png);
    }

    [Fact]
    public void Detects_webp()
    {
        // RIFF <4-byte size> WEBP
        byte[] header = [.. "RIFF"u8.ToArray(), 0x24, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8.ToArray()];

        ImageFormat.Detect(WithPadding(header)).Should().Be(ImageFormat.Webp);
    }

    [Fact]
    public void Riff_that_is_not_webp_is_rejected()
    {
        // A WAV file is also RIFF.
        byte[] header = [.. "RIFF"u8.ToArray(), 0x24, 0x00, 0x00, 0x00, .. "WAVEfmt "u8.ToArray()];

        ImageFormat.Detect(WithPadding(header)).Should().BeNull();
    }

    [Fact]
    public void Gif_is_rejected()
    {
        ImageFormat.Detect(WithPadding("GIF89a"u8.ToArray())).Should().BeNull();
    }

    [Fact]
    public void Text_is_rejected()
    {
        ImageFormat.Detect(Encoding.UTF8.GetBytes("isto não é uma imagem, é texto puro")).Should().BeNull();
    }

    [Fact]
    public void Svg_is_rejected()
    {
        ImageFormat.Detect(Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>")).Should().BeNull();
    }

    [Fact]
    public void Empty_file_is_rejected()
    {
        ImageFormat.Detect([]).Should().BeNull();
    }

    [Theory]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xD8 })]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 0x24, 0x00, 0x00, 0x00, 0x57, 0x45 })]
    public void Truncated_headers_are_rejected(byte[] header)
    {
        ImageFormat.Detect(header).Should().BeNull();
    }

    [Fact]
    public void Formats_expose_extension_and_content_type()
    {
        ImageFormat.Jpeg.Should().Be(new ImageFormat("jpg", "image/jpeg"));
        ImageFormat.Png.Should().Be(new ImageFormat("png", "image/png"));
        ImageFormat.Webp.Should().Be(new ImageFormat("webp", "image/webp"));
    }
}
