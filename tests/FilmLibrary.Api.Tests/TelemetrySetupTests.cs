using FilmLibrary.Api.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FilmLibrary.Api.Tests;

/// <summary>Application Insights shows the OpenTelemetry service name as the cloud role (application map, workbook filters).</summary>
[Collection(nameof(EnvironmentVariableTests))]
public class TelemetrySetupTests
{
    // App Service sets this; Azure Monitor's App Service detector turns it into the service name.
    private static readonly Dictionary<string, string> AppService = new() { ["WEBSITE_SITE_NAME"] = "app-filmlib-api-dev-abc123" };

    // Only the name is asserted. The instance id comes from the distro's detectors and depends on where the test runs:
    // on Microsoft-hosted agents (Azure VMs) the VM detector reaches the metadata endpoint and sets the VM's id.
    [Fact]
    public void On_App_Service_the_api_reports_as_FilmLibrary_Api()
    {
        var resource = AzureMonitorResourceWith(AppService);

        Assert.Equal(TelemetrySetup.ServiceName, Attribute(resource, "service.name"));
    }

    private static Resource AzureMonitorResourceWith(Dictionary<string, string> environment)
    {
        var previous = environment.Keys.ToDictionary(key => key, Environment.GetEnvironmentVariable);
        foreach (var (key, value) in environment)
            Environment.SetEnvironmentVariable(key, value);
        try
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000",
            }).Build();
            using var services = new ServiceCollection().AddLogging().AddFilmLibraryTelemetry(configuration).BuildServiceProvider();
            return services.GetRequiredService<TracerProvider>().GetResource();
        }
        finally
        {
            foreach (var (key, value) in previous)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static object? Attribute(Resource resource, string key) => resource.Attributes.Single(a => a.Key == key).Value;
}

/// <summary>Tests that change process-wide environment variables run one at a time.</summary>
[CollectionDefinition(nameof(EnvironmentVariableTests), DisableParallelization = true)]
public class EnvironmentVariableTests;
