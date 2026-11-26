using System.Text.Json.Nodes;
using FluentAssertions;

namespace EasyMenu.IntegrationTests.Contract;

/// <summary>
/// Proves that <see cref="OpenApiContractComparer"/> actually detects divergences, using in-memory documents
/// (no API host / database needed). The "generated" document mimics what ASP.NET Core emits (OpenAPI 3.1,
/// different component names, extra text/plain and text/json media types).
/// </summary>
public sealed class OpenApiContractComparerTests
{
    private const string ContractYaml =
        """
        openapi: 3.1.0
        info: { title: Sample, version: 1.0.0 }
        paths:
          /items:
            post:
              operationId: createItem
              requestBody:
                required: true
                content:
                  application/json:
                    schema: { $ref: "#/components/schemas/CreateItemRequest" }
              responses:
                "201":
                  description: Created
                  content:
                    application/json:
                      schema: { $ref: "#/components/schemas/Item" }
                "422":
                  description: Invalid
          /items/{id}:
            get:
              operationId: getItem
              parameters:
                - name: id
                  in: path
                  required: true
                  schema: { type: string, format: uuid }
              responses:
                "200":
                  description: OK
                  content:
                    application/json:
                      schema: { $ref: "#/components/schemas/Item" }
                "404":
                  description: Not found
          /orders:
            get:
              x-pending: true
              operationId: listOrders
              responses:
                "200":
                  description: OK
        components:
          schemas:
            Category:
              type: string
              enum: [food, drink]
            Item:
              type: object
              required: [id, name, priceCents, category]
              properties:
                id: { type: string, format: uuid }
                name: { type: string }
                priceCents: { type: integer, format: int64 }
                promoPriceCents: { type: integer, format: int64, nullable: true }
                category: { $ref: "#/components/schemas/Category" }
                tags:
                  type: array
                  items: { type: string }
            CreateItemRequest:
              type: object
              required: [name, priceCents]
              properties:
                name: { type: string }
                priceCents: { type: integer, format: int64 }
        """;

    private const string GeneratedJson =
        """
        {
          "openapi": "3.1.1",
          "paths": {
            "/items/{id}": {
              "get": {
                "parameters": [
                  { "name": "id", "in": "path", "required": true, "schema": { "type": "string", "format": "uuid" } }
                ],
                "responses": {
                  "200": {
                    "description": "OK",
                    "content": {
                      "text/plain": { "schema": { "$ref": "#/components/schemas/ItemResponse" } },
                      "application/json": { "schema": { "$ref": "#/components/schemas/ItemResponse" } },
                      "text/json": { "schema": { "$ref": "#/components/schemas/ItemResponse" } }
                    }
                  },
                  "404": { "description": "Not Found" }
                }
              }
            },
            "/items": {
              "post": {
                "requestBody": {
                  "required": true,
                  "content": {
                    "application/json": { "schema": { "$ref": "#/components/schemas/CreateItemCommand" } },
                    "text/json": { "schema": { "$ref": "#/components/schemas/CreateItemCommand" } }
                  }
                },
                "responses": {
                  "201": {
                    "description": "Created",
                    "content": { "application/json": { "schema": { "$ref": "#/components/schemas/ItemResponse" } } }
                  },
                  "422": { "description": "Unprocessable Entity" }
                }
              }
            }
          },
          "components": {
            "schemas": {
              "ItemCategory": { "enum": ["drink", "food"], "type": "string" },
              "ItemResponse": {
                "required": ["category", "id", "name", "priceCents"],
                "type": "object",
                "properties": {
                  "promoPriceCents": { "type": ["null", "integer"], "format": "int64" },
                  "priceCents": { "type": "integer", "format": "int64" },
                  "name": { "type": "string" },
                  "id": { "type": "string", "format": "uuid" },
                  "category": { "$ref": "#/components/schemas/ItemCategory" },
                  "tags": { "type": "array", "items": { "type": "string" } }
                }
              },
              "CreateItemCommand": {
                "required": ["name", "priceCents"],
                "type": "object",
                "properties": {
                  "name": { "type": "string" },
                  "priceCents": { "type": "integer", "format": "int64" }
                }
              }
            }
          }
        }
        """;

    private readonly JsonObject _contract = OpenApiDocuments.ParseYaml(ContractYaml);
    private readonly JsonObject _generated = OpenApiDocuments.ParseJson(GeneratedJson);

    private JsonObject GeneratedSchema(string name) => (JsonObject)_generated["components"]!["schemas"]![name]!;

    private JsonObject GeneratedProperty(string schema, string property) =>
        (JsonObject)GeneratedSchema(schema)["properties"]![property]!;

    private IReadOnlyList<string> Compare() => OpenApiContractComparer.Compare(_contract, _generated);

    [Fact]
    public void Equivalent_documents_have_no_divergences()
    {
        // Different component names, property order, 3.0 vs 3.1 nullability, extra media types and
        // a missing x-pending operation (GET /orders) are all acceptable.
        Compare().Should().BeEmpty();
    }

    [Fact]
    public void Oneof_null_wrapper_is_treated_as_nullable()
    {
        GeneratedSchema("ItemResponse")["properties"]!["promoPriceCents"] = JsonNode.Parse(
            """{ "oneOf": [ { "type": "null" }, { "type": "integer", "format": "int64" } ] }""");

        Compare().Should().BeEmpty();
    }

    [Fact]
    public void Property_missing_in_the_api_is_a_divergence()
    {
        GeneratedSchema("ItemResponse")["properties"]!.AsObject().Remove("tags");

        // Item is used by two operations, so the divergence is reported for each of them.
        Compare().Should().BeEquivalentTo(
            "GET /items/{id} → response 200 application/json.tags: property declared in the contract but missing in the API.",
            "POST /items → response 201 application/json.tags: property declared in the contract but missing in the API.");
    }

    [Fact]
    public void Property_not_declared_in_the_contract_is_a_divergence()
    {
        GeneratedSchema("CreateItemCommand")["properties"]!["establishmentId"] = JsonNode.Parse("""{ "type": "string" }""");

        Compare().Should().ContainSingle().Which.Should()
            .Contain("POST /items → request body application/json.establishmentId")
            .And.Contain("not declared in the contract");
    }

    [Fact]
    public void Monetary_value_as_number_instead_of_integer_is_a_divergence()
    {
        GeneratedProperty("ItemResponse", "priceCents")["type"] = "number";
        GeneratedProperty("ItemResponse", "priceCents").Remove("format");

        var divergences = Compare();

        divergences.Should().Contain(d => d.Contains("priceCents") && d.Contains("type differs — contract integer, API number"));
        divergences.Should().Contain(d => d.Contains("priceCents") && d.Contains("format differs — contract int64, API (none)"));
    }

    [Fact]
    public void Integer_accepting_strings_is_a_type_divergence()
    {
        // ASP.NET Core emits this when JsonNumberHandling.AllowReadingFromString is on.
        GeneratedProperty("CreateItemCommand", "priceCents")["type"] = new JsonArray("integer", "string");

        Compare().Should().ContainSingle().Which.Should()
            .Contain("POST /items → request body application/json.priceCents")
            .And.Contain("type differs — contract integer, API integer|string");
    }

    [Fact]
    public void Int32_instead_of_int64_is_a_format_divergence()
    {
        GeneratedProperty("CreateItemCommand", "priceCents")["format"] = "int32";

        Compare().Should().ContainSingle().Which.Should().Contain("format differs — contract int64, API int32");
    }

    [Fact]
    public void Required_divergence_is_reported()
    {
        GeneratedSchema("CreateItemCommand")["required"] = new JsonArray("name");

        Compare().Should().ContainSingle().Which.Should()
            .Contain("POST /items → request body application/json")
            .And.Contain("required differs")
            .And.Contain("missing in API: [priceCents]");
    }

    [Fact]
    public void Nullable_divergence_is_reported()
    {
        GeneratedProperty("ItemResponse", "promoPriceCents")["type"] = "integer";

        Compare().Should().OnlyContain(d => d.Contains("promoPriceCents") && d.Contains("'nullable' differs — contract true, API false"))
            .And.NotBeEmpty();
    }

    [Fact]
    public void Enum_divergence_is_reported()
    {
        GeneratedSchema("ItemCategory")["enum"] = new JsonArray("food", "drink", "dessert");

        Compare().Should().NotBeEmpty().And.OnlyContain(d =>
            d.Contains(".category") && d.Contains("enum differs — contract [\"drink\", \"food\"], API [\"dessert\", \"drink\", \"food\"]"));
    }

    [Fact]
    public void Missing_enum_in_the_api_is_reported()
    {
        GeneratedSchema("ItemCategory").Remove("enum");

        Compare().Should().NotBeEmpty().And.OnlyContain(d => d.Contains(".category") && d.Contains("enum differs"));
    }

    [Fact]
    public void Status_code_divergence_is_reported()
    {
        var responses = (JsonObject)_generated["paths"]!["/items/{id}"]!["get"]!["responses"]!;
        responses.Remove("404");
        responses["500"] = JsonNode.Parse("""{ "description": "Error" }""");

        Compare().Should().ContainSingle().Which.Should()
            .Contain("GET /items/{id} → responses")
            .And.Contain("missing in API: [404]")
            .And.Contain("not in contract: [500]");
    }

    [Fact]
    public void Response_media_type_missing_in_the_api_is_reported()
    {
        var content = (JsonObject)_generated["paths"]!["/items"]!["post"]!["responses"]!["201"]!["content"]!;
        content.Remove("application/json");
        content["text/plain"] = JsonNode.Parse("""{ "schema": { "type": "string" } }""");

        Compare().Should().ContainSingle().Which.Should()
            .Contain("POST /items → response 201").And.Contain("'application/json' declared in the contract but missing in the API");
    }

    [Fact]
    public void Body_returned_where_the_contract_declares_none_is_reported()
    {
        _generated["paths"]!["/items/{id}"]!["get"]!["responses"]!["404"]!["content"] =
            JsonNode.Parse("""{ "application/json": { "schema": { "type": "object" } } }""");

        Compare().Should().ContainSingle().Which.Should()
            .Contain("GET /items/{id} → response 404").And.Contain("the contract declares none");
    }

    [Fact]
    public void Parameter_schema_divergence_is_reported()
    {
        _generated["paths"]!["/items/{id}"]!["get"]!["parameters"]![0]!["schema"] =
            JsonNode.Parse("""{ "type": "integer", "format": "int32" }""");

        var divergences = Compare();

        divergences.Should().HaveCount(2).And.OnlyContain(d => d.StartsWith("GET /items/{id} → parameter path:id"));
    }

    [Fact]
    public void Operation_not_described_in_the_contract_is_reported()
    {
        _generated["paths"]!["/admin/secret"] = JsonNode.Parse("""{ "delete": { "responses": { "204": { "description": "No Content" } } } }""");

        Compare().Should().ContainSingle().Which.Should()
            .Be("DELETE /admin/secret: implemented by the API but not described in contracts/openapi.yaml.");
    }

    [Fact]
    public void Contract_operation_without_x_pending_must_be_implemented()
    {
        ((JsonObject)_generated["paths"]!).Remove("/items/{id}");

        Compare().Should().ContainSingle().Which.Should()
            .Be("GET /items/{id}: described in the contract (without x-pending) but not implemented by the API.");
    }

    [Fact]
    public void Implemented_operation_still_marked_x_pending_fails_with_a_clear_message()
    {
        _generated["paths"]!["/orders"] = JsonNode.Parse("""{ "get": { "responses": { "200": { "description": "OK" } } } }""");

        Compare().Should().ContainSingle().Which.Should().Contain("remova x-pending de GET /orders");
    }

    [Fact]
    public void All_divergences_are_listed_not_only_the_first()
    {
        GeneratedProperty("ItemResponse", "priceCents")["type"] = "number";
        GeneratedSchema("CreateItemCommand")["required"] = new JsonArray();
        ((JsonObject)_generated["paths"]!["/items"]!["post"]!["responses"]!).Remove("422");
        _generated["paths"]!["/orders"] = JsonNode.Parse("""{ "get": { "responses": { "200": { "description": "OK" } } } }""");

        var divergences = Compare();

        divergences.Should().Contain(d => d.Contains("priceCents") && d.Contains("type differs"));
        divergences.Should().Contain(d => d.Contains("required differs"));
        divergences.Should().Contain(d => d.Contains("missing in API: [422]"));
        divergences.Should().Contain(d => d.Contains("remova x-pending de GET /orders"));
    }
}
