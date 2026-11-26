using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace EasyMenu.Api.Serialization;

/// <summary>
/// Binds enums in route and query values by their snake_case names, as in JSON and in the contract
/// (e.g. <c>picked_up</c>). Anything else, numbers included, is a model error (400 <c>validation_failed</c>).
/// </summary>
public sealed class SnakeCaseEnumModelBinder(Type enumType) : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        var value = result.FirstValue;
        var match = Enum.GetNames(enumType).FirstOrDefault(name =>
            string.Equals(JsonNamingPolicy.SnakeCaseLower.ConvertName(name), value, StringComparison.Ordinal));

        if (match is null)
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "The value is not valid.");
            bindingContext.Result = ModelBindingResult.Failed();
        }
        else
        {
            bindingContext.Result = ModelBindingResult.Success(Enum.Parse(enumType, match));
        }

        return Task.CompletedTask;
    }
}

/// <summary>Uses <see cref="SnakeCaseEnumModelBinder"/> for enum parameters bound from the route or the query.</summary>
public sealed class SnakeCaseEnumModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        var source = context.BindingInfo.BindingSource;
        var fromRouteOrQuery = source is not null
            && (source.CanAcceptDataFrom(BindingSource.Query) || source.CanAcceptDataFrom(BindingSource.Path));
        return type.IsEnum && fromRouteOrQuery ? new SnakeCaseEnumModelBinder(type) : null;
    }
}
