using System.Net;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Menu;

/// <summary>
/// Hardened static serving of uploaded photos under <c>/media</c> (from <c>Storage:ImagesPath</c>): only image
/// types, nosniff, long immutable cache, no directory listing and no path traversal.
/// Files are written directly to <see cref="EasyMenuApiFactory.ImagesPath"/>.
/// </summary>
public sealed class MediaStaticFilesTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] WebpBytes = "RIFF\u001a\0\0\0WEBPVP8 "u8.ToArray();

    private readonly Guid _folder = Guid.NewGuid();

    private string WriteFile(string fileName, byte[] content)
    {
        var directory = Path.Combine(Factory.ImagesPath, _folder.ToString());
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, fileName), content);
        return $"/media/{_folder}/{fileName}";
    }

    [Fact]
    public async Task Jpeg_is_served_with_nosniff_and_immutable_cache()
    {
        var url = WriteFile("a.jpg", JpegBytes);

        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.ToString().Should().Contain("immutable");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(JpegBytes);
    }

    [Theory]
    [InlineData("a.jpeg", "image/jpeg")]
    [InlineData("a.png", "image/png")]
    [InlineData("a.webp", "image/webp")]
    public async Task Other_image_types_are_served_with_the_right_content_type(string fileName, string mediaType)
    {
        var url = WriteFile(fileName, fileName.EndsWith(".png") ? PngBytes : fileName.EndsWith(".webp") ? WebpBytes : JpegBytes);

        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be(mediaType);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
    }

    [Theory]
    [InlineData("x.html")]
    [InlineData("x.svg")]
    [InlineData("x.txt")]
    [InlineData("x")]
    public async Task Non_image_files_are_not_served(string fileName)
    {
        // Content that a browser could execute if it were served.
        var url = WriteFile(fileName, "<script>alert(1)</script>"u8.ToArray());

        var response = await Client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Media_root_has_no_directory_listing()
    {
        WriteFile("a.jpg", JpegBytes);

        (await Client.GetAsync("/media/")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Media_folder_has_no_directory_listing()
    {
        WriteFile("a.jpg", JpegBytes);

        (await Client.GetAsync($"/media/{_folder}/")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Encoded_path_traversal_to_appsettings_returns_404()
    {
        var response = await Client.GetAsync("/media/..%2F..%2Fappsettings.json");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Encoded_path_traversal_to_an_image_outside_the_media_root_returns_404()
    {
        // An image-typed file that exists right above Storage:ImagesPath must not be reachable.
        var outside = Path.Combine(Directory.GetParent(Factory.ImagesPath)!.FullName, $"outside-{_folder}.jpg");
        File.WriteAllBytes(outside, JpegBytes);
        WriteFile("a.jpg", JpegBytes); // keeps /media itself working

        var response = await Client.GetAsync($"/media/..%2Foutside-{_folder}.jpg");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
