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
        // Enums travel as snake_case strings and are documented as `type: string` + `enum`.
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        // Numbers (e.g. amounts in cents) are plain JSON numbers: `type: integer, format: int64`;
        // a number sent as a string is rejected (400 validation_failed).
        options.NumberHandling = JsonNumberHandling.Strict;
        // Null fields are omitted from responses.
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    }
}
