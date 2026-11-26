using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;

namespace EasyMenu.Api.Media;

/// <summary>
/// Hardened static serving of uploaded photos under <c>/media</c> (research R4): only JPEG/PNG/WebP, nosniff,
/// long immutable cache and no directory listing. Traversal outside the root is blocked by the file provider.
/// </summary>
public static class MediaStaticFileOptions
{
    public const string RequestPath = "/media";

    public static StaticFileOptions Create(string imagesPath)
    {
        var contentTypes = new FileExtensionContentTypeProvider(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp",
        });

        return new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(imagesPath),
            RequestPath = RequestPath,
            ContentTypeProvider = contentTypes,
            ServeUnknownFileTypes = false,
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.Headers;
                headers[HeaderNames.XContentTypeOptions] = "nosniff";
                // File names are unique per upload, so the content never changes.
                headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
            },
        };
    }
}
