using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FilmLibrary.Api.Observability;

public static class TelemetrySetup
{
    /// <summary>OpenTelemetry service name; Application Insights shows it as the cloud role on the application map.</summary>
    public const string ServiceName = "FilmLibrary.Api";

    /// <summary>
    /// OpenTelemetry traces, metrics and logs, exported to Application Insights in Azure
    /// (APPLICATIONINSIGHTS_CONNECTION_STRING) or to any OTLP endpoint locally (OTEL_EXPORTER_OTLP_ENDPOINT).
    /// With neither set, nothing is exported.
    /// </summary>
    public static IServiceCollection AddFilmLibraryTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<FilmMetrics>();

        // Cosmos DB reports each operation through the Azure SDK's ActivitySource, which is opt-in.
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        // Health probes run every minute in Azure; tracing them would only add noise and ingestion cost.
        services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/healthz"));

        var telemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(ServiceName))
            .WithTracing(tracing => tracing.AddSource("Azure.Cosmos.Operation"))
            .WithMetrics(metrics => metrics.AddMeter(FilmMetrics.MeterName));

        if (!string.IsNullOrEmpty(configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            // The distro adds ASP.NET Core, HttpClient and log collection, plus live metrics.
            telemetry.UseAzureMonitor();
        }
        else if (!string.IsNullOrEmpty(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry
                .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
                .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddMeter("System.Runtime"))
                .WithLogging()
                .UseOtlpExporter();
        }

        return services;
    }
}
