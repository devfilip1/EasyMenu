using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EasyMenu.Api.OpenApi;

/// <summary>
/// The generator ignores <see cref="EmailAddressAttribute"/>; documents such properties as
/// <c>format: email</c>, as the contract does.
/// </summary>
internal sealed class EmailFormatSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var isEmail = context.JsonPropertyInfo?.AttributeProvider?
            .IsDefined(typeof(EmailAddressAttribute), inherit: true) == true;
        if (isEmail && schema.Format is null)
        {
            schema.Format = "email";
        }

        return Task.CompletedTask;
    }
}
