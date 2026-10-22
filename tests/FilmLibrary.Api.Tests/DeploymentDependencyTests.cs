using System.Text.Json.Nodes;

namespace FilmLibrary.Api.Tests;

/// <summary>
/// Checks the API's own dependency manifest (what gets deployed), not this test project's. The test project pulls in
/// extra packages transitively, which can hide a missing runtime dependency that only fails in Azure.
/// </summary>
public class DeploymentDependencyTests
{
    [Fact]
    public void Api_ships_NewtonsoftJson_which_the_Cosmos_SDK_loads_at_runtime()
    {
        // Microsoft.Azure.Cosmos needs Newtonsoft.Json to create a client, even when configured for System.Text.Json.
        var deps = JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "FilmLibrary.Api.deps.json")))!;
        var libraries = deps["libraries"]!.AsObject().Select(l => l.Key);

        Assert.Contains(libraries, name => name.StartsWith("Newtonsoft.Json/", StringComparison.Ordinal));
    }
}
