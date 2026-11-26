using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using EasyMenu.IntegrationTests.Infrastructure;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Menu;

/// <summary>
/// PUT/DELETE /api/menu-items/{id}/photo (research R4): JPEG/PNG/WebP checked by magic bytes, 5 MB max (413
/// <c>image_too_large</c> also when the body exceeds the endpoint limit), stored under Storage:ImagesPath.
/// </summary>
public sealed partial class MenuItemPhotoTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    private const int MegaByte = 1024 * 1024;

    private static readonly byte[] JpegHeader = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] PngHeader = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static byte[] Image(byte[] header, int size = 2048)
    {
        var bytes = new byte[size];
        header.CopyTo(bytes, 0);
        bytes[^2] = 0xFF;
        bytes[^1] = 0xD9;
        return bytes;
    }

    private static MultipartFormDataContent Form(byte[] content, string fileName, string contentType, string field = "file")
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, field, fileName } };
    }

    private static string PhotoRoute(Guid itemId) => $"/api/menu-items/{itemId}/photo";

    private string DiskPath(string photoUrl)
    {
        photoUrl.Should().StartWith("/media/");
        return Path.Combine([Factory.ImagesPath, .. photoUrl["/media/".Length..].Split('/')]);
    }

    private async Task<(RegisteredOwner Owner, DefaultMenu Menu, HttpClient Client)> ArrangeAsync()
    {
        var owner = await Api.RegisterOwnerAsync();
        var menu = await Api.CreateMenuAsync(owner.Token);
        return (owner, menu, Api.CreateClient(owner.Token));
    }

    private static async Task<MenuItemDto> UploadAsync(HttpClient client, Guid itemId, byte[] content, string fileName, string contentType) =>
        await ApiDriver.ReadSuccessAsync<MenuItemDto>(
            await client.PutAsync(PhotoRoute(itemId), Form(content, fileName, contentType)), "upload photo");

    [GeneratedRegex(@"^/media/(?<establishment>[0-9a-fA-F-]{36})/[^/]+\.jpg$")]
    private static partial Regex JpegPhotoUrl();

    [Fact]
    public async Task Jpeg_upload_returns_200_with_photo_url_that_is_served_as_image_jpeg()
    {
        var (owner, menu, client) = await ArrangeAsync();
        var jpeg = Image(JpegHeader);

        var response = await client.PutAsync(PhotoRoute(menu.Batata.Id), Form(jpeg, "batata.jpg", "image/jpeg"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var item = await ApiDriver.ReadSuccessAsync<MenuItemDto>(response, "upload");
        item.Should().Be(menu.Batata with { PhotoUrl = item.PhotoUrl });
        item.PhotoUrl.Should().MatchRegex(JpegPhotoUrl());
        JpegPhotoUrl().Match(item.PhotoUrl!).Groups["establishment"].Value.Should().Be(owner.EstablishmentId.ToString());
        File.Exists(DiskPath(item.PhotoUrl!)).Should().BeTrue();

        var photo = await Client.GetAsync(item.PhotoUrl);
        photo.StatusCode.Should().Be(HttpStatusCode.OK);
        photo.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await photo.Content.ReadAsByteArrayAsync()).Should().Equal(jpeg);

        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(await client.GetAsync($"/api/menu-items/{menu.Batata.Id}"), "get"))
            .PhotoUrl.Should().Be(item.PhotoUrl);
    }

    [Fact]
    public async Task Extension_follows_the_detected_type_not_the_file_name()
    {
        var (_, menu, client) = await ArrangeAsync();

        var item = await UploadAsync(client, menu.Batata.Id, Image(PngHeader), "foto.jpg", "image/jpeg");

        item.PhotoUrl.Should().EndWith(".png");
    }

    [Fact]
    public async Task New_upload_replaces_the_photo_and_deletes_the_old_file()
    {
        var (_, menu, client) = await ArrangeAsync();
        var first = await UploadAsync(client, menu.Batata.Id, Image(JpegHeader), "a.jpg", "image/jpeg");

        var second = await UploadAsync(client, menu.Batata.Id, Image(PngHeader), "b.png", "image/png");

        second.PhotoUrl.Should().NotBeNull().And.NotBe(first.PhotoUrl);
        File.Exists(DiskPath(second.PhotoUrl!)).Should().BeTrue();
        File.Exists(DiskPath(first.PhotoUrl!)).Should().BeFalse("the replaced photo is removed from disk");
    }

    [Fact]
    public async Task File_between_five_and_six_megabytes_returns_413_image_too_large()
    {
        var (_, menu, client) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(
            await client.PutAsync(PhotoRoute(menu.Batata.Id), Form(Image(JpegHeader, (int)(5.5 * MegaByte)), "big.jpg", "image/jpeg")));

        problem.Status.Should().Be(413, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("image_too_large");
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(await client.GetAsync($"/api/menu-items/{menu.Batata.Id}"), "get"))
            .PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Body_above_the_endpoint_limit_also_returns_413_image_too_large()
    {
        var (_, menu, client) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(
            await client.PutAsync(PhotoRoute(menu.Batata.Id), Form(Image(JpegHeader, 7 * MegaByte), "huge.jpg", "image/jpeg")));

        problem.Status.Should().Be(413, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("image_too_large");
    }

    [Fact]
    public async Task Text_disguised_as_png_returns_400_invalid_image_type()
    {
        var (_, menu, client) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await client.PutAsync(
            PhotoRoute(menu.Batata.Id), Form("isto é texto, não uma imagem"u8.ToArray(), "foto.png", "image/png")));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("invalid_image_type");
        Directory.Exists(Factory.ImagesPath).Should().BeTrue();
    }

    [Fact]
    public async Task Gif_returns_400_invalid_image_type()
    {
        var (_, menu, client) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await client.PutAsync(
            PhotoRoute(menu.Batata.Id), Form(Image("GIF89a"u8.ToArray()), "foto.gif", "image/gif")));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.Code.Should().Be("invalid_image_type");
    }

    [Fact]
    public async Task Missing_file_returns_400()
    {
        var (_, menu, client) = await ArrangeAsync();

        var problem = await ApiDriver.ReadProblemAsync(await client.PutAsync(
            PhotoRoute(menu.Batata.Id), Form(Image(JpegHeader), "foto.jpg", "image/jpeg", field: "outro")));

        problem.Status.Should().Be(400, problem.RawBody);
        problem.MediaType.Should().Be("application/problem+json");
        problem.Code.Should().Be("validation_failed");
    }

    [Fact]
    public async Task Delete_photo_returns_204_clears_photo_url_and_deletes_the_file()
    {
        var (_, menu, client) = await ArrangeAsync();
        var uploaded = await UploadAsync(client, menu.Batata.Id, Image(JpegHeader), "a.jpg", "image/jpeg");

        var response = await client.DeleteAsync(PhotoRoute(menu.Batata.Id));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(await client.GetAsync($"/api/menu-items/{menu.Batata.Id}"), "get"))
            .PhotoUrl.Should().BeNull();
        File.Exists(DiskPath(uploaded.PhotoUrl!)).Should().BeFalse();
    }

    [Fact]
    public async Task Deleting_the_item_deletes_the_photo_file()
    {
        var (_, menu, client) = await ArrangeAsync();
        var uploaded = await UploadAsync(client, menu.Batata.Id, Image(JpegHeader), "a.jpg", "image/jpeg");

        (await client.DeleteAsync($"/api/menu-items/{menu.Batata.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        File.Exists(DiskPath(uploaded.PhotoUrl!)).Should().BeFalse();
    }

    [Fact]
    public async Task Kitchen_gets_403()
    {
        var (owner, menu, _) = await ArrangeAsync();
        var kitchen = Api.CreateClient(await Api.CreateKitchenTokenAsync(owner.EstablishmentId));

        (await kitchen.PutAsync(PhotoRoute(menu.Batata.Id), Form(Image(JpegHeader), "a.jpg", "image/jpeg")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await kitchen.DeleteAsync(PhotoRoute(menu.Batata.Id))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var (_, menu, _) = await ArrangeAsync();

        (await Client.PutAsync(PhotoRoute(menu.Batata.Id), Form(Image(JpegHeader), "a.jpg", "image/jpeg")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Client.DeleteAsync(PhotoRoute(menu.Batata.Id))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Isolation_upload_on_item_of_a_with_token_of_b_returns_404()
    {
        var (_, menuA, clientA) = await ArrangeAsync();
        var ownerB = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token)
            .PutAsync(PhotoRoute(menuA.Batata.Id), Form(Image(JpegHeader), "a.jpg", "image/jpeg")));

        problem.Status.Should().Be(404, problem.RawBody);
        problem.Code.Should().Be("not_found");
        (await ApiDriver.ReadSuccessAsync<MenuItemDto>(await clientA.GetAsync($"/api/menu-items/{menuA.Batata.Id}"), "get"))
            .PhotoUrl.Should().BeNull();
    }

    [Fact]
    public async Task Isolation_delete_photo_of_a_with_token_of_b_returns_404_and_keeps_the_file()
    {
        var (_, menuA, clientA) = await ArrangeAsync();
        var uploaded = await UploadAsync(clientA, menuA.Batata.Id, Image(JpegHeader), "a.jpg", "image/jpeg");
        var ownerB = await Api.RegisterOwnerAsync();

        var problem = await ApiDriver.ReadProblemAsync(await Api.CreateClient(ownerB.Token).DeleteAsync(PhotoRoute(menuA.Batata.Id)));

        problem.Status.Should().Be(404, problem.RawBody);
        File.Exists(DiskPath(uploaded.PhotoUrl!)).Should().BeTrue();
    }
}
