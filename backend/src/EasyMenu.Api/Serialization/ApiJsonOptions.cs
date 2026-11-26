using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyMenu.Api.Serialization;

/// <summary>
/// JSON conventions shared by MVC serialization and the OpenAPI generator (which reads the
/// Http.Json options), so the generated document matches contracts/openapi.yaml.
/// </summary>
public static class ApiJsonOptions
{
    public static void Apply(JsonSerializerOptions options)
    {
        // Enums travel as camelCase strings and are documented as `type: string` + `enum`.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        // Numbers (e.g. amounts in cents) are plain JSON numbers: `type: integer, format: int64`.
        options.NumberHandling = JsonNumberHandling.Strict;
    }
}
