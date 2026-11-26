using System.Text.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EasyMenu.Infrastructure.Persistence;

/// <summary>Stores enums as snake_case text, the same values the contract uses (e.g. Role.Owner → "owner").</summary>
internal static class SnakeCaseEnumConverter
{
    public static ValueConverter For(Type enumType) =>
        (ValueConverter)Activator.CreateInstance(typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType))!;

    public static string ToSnakeCase(string name) => JsonNamingPolicy.SnakeCaseLower.ConvertName(name);
}

internal sealed class SnakeCaseEnumConverter<TEnum>() : ValueConverter<TEnum, string>(
    value => ToProvider(value),
    value => FromProvider(value))
    where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => SnakeCaseEnumConverter.ToSnakeCase(v.ToString()));

    private static readonly Dictionary<string, TEnum> Values = Names.ToDictionary(kv => kv.Value, kv => kv.Key);

    private static string ToProvider(TEnum value) => Names[value];

    private static TEnum FromProvider(string value) => Values[value];
}
