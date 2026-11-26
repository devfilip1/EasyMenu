using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace EasyMenu.IntegrationTests.Contract;

/// <summary>Loads OpenAPI documents (YAML or JSON) as <see cref="JsonObject"/> trees.</summary>
public static class OpenApiDocuments
{
    public static JsonObject LoadContract() => ParseYaml(File.ReadAllText(FindContractFile()));

    public static JsonObject ParseJson(string json) =>
        JsonNode.Parse(json) as JsonObject ?? throw new FormatException("OpenAPI JSON root must be an object.");

    /// <summary>Parses YAML into JSON nodes. Plain scalars become bool/number/null when they look like one.</summary>
    public static JsonObject ParseYaml(string yaml)
    {
        var stream = new YamlStream();
        using (var reader = new StringReader(yaml))
        {
            stream.Load(reader);
        }

        return ToJson(stream.Documents[0].RootNode) as JsonObject
            ?? throw new FormatException("OpenAPI YAML root must be a mapping.");
    }

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode map => new JsonObject(map.Children.Select(kv =>
            KeyValuePair.Create(((YamlScalarNode)kv.Key).Value!, ToJson(kv.Value)))),
        YamlSequenceNode seq => new JsonArray(seq.Children.Select(ToJson).ToArray()),
        YamlScalarNode scalar => ScalarToJson(scalar),
        _ => throw new FormatException($"Unsupported YAML node {node.GetType().Name} at {node.Start}."),
    };

    private static JsonNode? ScalarToJson(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? string.Empty;
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(value);
        }

        return value switch
        {
            "" or "~" or "null" or "Null" or "NULL" => null,
            "true" or "True" or "TRUE" => JsonValue.Create(true),
            "false" or "False" or "FALSE" => JsonValue.Create(false),
            _ when long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var l)
                => JsonValue.Create(l),
            _ when double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                => JsonValue.Create(d),
            _ => JsonValue.Create(value),
        };
    }

    /// <summary>Walks up from the test output directory and, as a fallback, from this source file.</summary>
    private static string FindContractFile([CallerFilePath] string sourceFile = "")
    {
        string?[] starts = [AppContext.BaseDirectory, Path.GetDirectoryName(sourceFile)];
        foreach (var start in starts.Where(s => !string.IsNullOrEmpty(s)))
        {
            for (var dir = new DirectoryInfo(start!); dir is not null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "contracts", "openapi.yaml");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        throw new FileNotFoundException("contracts/openapi.yaml not found in any parent directory.");
    }
}
