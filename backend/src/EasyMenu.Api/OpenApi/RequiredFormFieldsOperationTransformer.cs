using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace EasyMenu.Api.OpenApi;

/// <summary>
/// The generator does not list required form fields (e.g. <c>[Required]</c> file uploads) in the multipart schema;
/// adds them to <c>required</c>, as the contract does.
/// </summary>
internal sealed class RequiredFormFieldsOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var requiredFields = context.Description.ParameterDescriptions
            .Where(p => (p.Source == BindingSource.Form || p.Source == BindingSource.FormFile)
                && (p.IsRequired || p.ModelMetadata?.IsRequired == true))
            .Select(p => p.Name)
            .ToList();
        if (requiredFields.Count == 0 || operation.RequestBody?.Content is not { } content)
        {
            return Task.CompletedTask;
        }

        foreach (var media in content.Values)
        {
            if (media.Schema is OpenApiSchema schema && schema.Properties is { } properties)
            {
                schema.Required ??= new HashSet<string>(StringComparer.Ordinal);
                foreach (var field in requiredFields.Where(properties.ContainsKey))
                {
                    schema.Required.Add(field);
                }
            }
        }

        return Task.CompletedTask;
    }
}
