using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EasyMenu.Api.OpenApi;

/// <summary>
/// Enums are serialized as strings (see ApiJsonOptions), but the generator emits only the
/// `enum` values without `type`. Declares them as `type: string` to match the contract.
/// </summary>
internal sealed class StringEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (type.IsEnum && schema.Type is null or 0)
        {
            schema.Type = JsonSchemaType.String;
        }

        return Task.CompletedTask;
    }
}
