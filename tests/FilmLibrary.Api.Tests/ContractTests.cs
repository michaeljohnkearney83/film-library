using System.Text.Json.Nodes;

namespace FilmLibrary.Api.Tests;

/// <summary>
/// api-spec/main.tsp is the source of truth for the contract. These tests compare the OpenAPI that TypeSpec
/// emits (api-spec/generated/openapi.json) with the OpenAPI the running API describes, so the C# side
/// can't drift from what the React app's generated types expect.
/// </summary>
public class ContractTests(FilmsApiFactory factory) : IClassFixture<FilmsApiFactory>
{
    private static readonly JsonNode Spec = JsonNode.Parse(File.ReadAllText(FindSpec()))!;

    public static TheoryData<string> Schemas => ["FilmSummary", "FilmDetails"];

    [Fact]
    public async Task Api_implements_exactly_the_specs_operations()
    {
        var api = await GetApiDocumentAsync();

        Assert.Equal(Operations(Spec), Operations(api));
    }

    [Theory]
    [InlineData("/api/films")]
    [InlineData("/api/films/{id}")]
    public async Task Api_declares_the_specs_responses(string path)
    {
        var api = await GetApiDocumentAsync();

        // 401 comes from the authentication middleware, which ASP.NET doesn't describe per endpoint.
        var expected = StatusCodes(Spec, path).Where(code => code != "401");
        Assert.Equal(expected, StatusCodes(api, path));
    }

    [Theory]
    [MemberData(nameof(Schemas))]
    public async Task Response_models_have_the_specs_fields_and_types(string schema)
    {
        var api = await GetApiDocumentAsync();

        Assert.Equal(Fields(Spec, schema), Fields(api, schema));
        Assert.Equal(Required(Spec, schema), Required(api, schema));
    }

    [Fact]
    public async Task Rating_has_the_specs_values()
    {
        var api = await GetApiDocumentAsync();

        Assert.Equal(EnumValues(Spec, "Rating"), EnumValues(api, "Rating"));
    }

    private async Task<JsonNode> GetApiDocumentAsync() =>
        JsonNode.Parse(await factory.CreateClient().GetStringAsync("/openapi/v1.json"))!;

    private static string[] Operations(JsonNode doc) =>
        [.. doc["paths"]!.AsObject()
            .SelectMany(path => path.Value!.AsObject()
                .Where(op => op.Key != "parameters")
                .Select(op => $"{op.Key.ToUpperInvariant()} {path.Key}"))
            .Order()];

    private static string[] StatusCodes(JsonNode doc, string path) =>
        [.. doc["paths"]![path]!["get"]!["responses"]!.AsObject().Select(r => r.Key).Order()];

    /// <summary>Field name → normalised type: a $ref's schema name, or the JSON type.</summary>
    private static Dictionary<string, string> Fields(JsonNode doc, string schema) =>
        doc["components"]!["schemas"]![schema]!["properties"]!.AsObject()
            .ToDictionary(p => p.Key, p => TypeOf(p.Value!));

    private static string[] Required(JsonNode doc, string schema) =>
        [.. doc["components"]!["schemas"]![schema]!["required"]!.AsArray().Select(r => (string)r!).Order()];

    private static string[] EnumValues(JsonNode doc, string schema) =>
        [.. doc["components"]!["schemas"]![schema]!["enum"]!.AsArray().Select(v => (string)v!)];

    private static string TypeOf(JsonNode property)
    {
        if (property["$ref"] is { } reference)
            return ((string)reference!).Split('/')[^1];

        // ASP.NET describes integers as ["integer", "string"] because web JSON options accept numbers in strings.
        return property["type"] switch
        {
            JsonArray types when types.Any(t => (string)t! == "integer") => "integer",
            JsonArray types => (string)types[0]!,
            var type => (string)type!,
        };
    }

    private static string FindSpec()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "api-spec", "generated", "openapi.json");
            if (File.Exists(candidate))
                return candidate;
        }

        throw new FileNotFoundException("api-spec/generated/openapi.json not found. Run `npm run build` in api-spec.");
    }
}
