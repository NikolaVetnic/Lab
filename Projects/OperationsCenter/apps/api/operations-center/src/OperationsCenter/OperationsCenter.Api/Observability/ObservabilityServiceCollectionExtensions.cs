using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OperationsCenter.Application.Observability;

namespace OperationsCenter.Api.Observability;

/// <summary>
/// Registers the Operations Center OpenTelemetry foundation: resource attributes,
/// automatic tracing/metrics instrumentation, custom application signals and OTLP export.
/// Telemetry is opt-in through <see cref="OpenTelemetryOptions.Enabled"/> so that the API
/// starts and serves traffic normally when telemetry is disabled or the collector is down.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>ActivitySource name emitted by the Npgsql driver for database command traces.</summary>
    private const string NpgsqlActivitySourceName = "Npgsql";

    private static readonly string[] HealthEndpointPaths = ["/health", "/ready"];

    public static IServiceCollection AddOperationsCenterObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        ILoggingBuilder logging,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection(OpenTelemetryOptions.SectionName);

        services.AddOptions<OpenTelemetryOptions>()
            .Bind(section)
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.OtlpEndpoint),
                "OpenTelemetry.OtlpEndpoint must be configured when telemetry is enabled.")
            .ValidateOnStart();

        var options = section.Get<OpenTelemetryOptions>() ?? new OpenTelemetryOptions();

        if (!options.Enabled)
        {
            return services;
        }

        void ConfigureResource(ResourceBuilder resource) => resource
            .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", environment.EnvironmentName)
            });

        services.AddOpenTelemetry()
            .ConfigureResource(ConfigureResource)
            .WithTracing(tracing => tracing
                .AddSource(OperationsCenterTelemetry.ActivitySourceName)
                .AddSource(NpgsqlActivitySourceName)
                .AddAspNetCoreInstrumentation(instrumentation =>
                {
                    instrumentation.RecordException = true;
                    instrumentation.Filter = context => !IsHealthEndpoint(context.Request.Path);
                })
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(exporter => ConfigureOtlp(exporter, options)))
            .WithMetrics(metrics => metrics
                .AddMeter(OperationsCenterTelemetry.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddOtlpExporter(exporter => ConfigureOtlp(exporter, options)));

        logging.AddOpenTelemetry(otel =>
        {
            otel.SetResourceBuilder(BuildResource(options, environment));
            otel.IncludeFormattedMessage = true;
            otel.IncludeScopes = true;
            otel.AddOtlpExporter(exporter => ConfigureOtlp(exporter, options));
        });

        return services;
    }

    private static ResourceBuilder BuildResource(OpenTelemetryOptions options, IHostEnvironment environment) =>
        ResourceBuilder.CreateDefault()
            .AddService(serviceName: options.ServiceName, serviceVersion: options.ServiceVersion)
            .AddAttributes(new KeyValuePair<string, object>[]
            {
                new("deployment.environment", environment.EnvironmentName)
            });

    private static void ConfigureOtlp(OtlpExporterOptions exporter, OpenTelemetryOptions options)
    {
        exporter.Endpoint = new Uri(options.OtlpEndpoint);

        if (string.Equals(options.OtlpProtocol, "http/protobuf", StringComparison.OrdinalIgnoreCase))
        {
            exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
        }
        else if (string.Equals(options.OtlpProtocol, "grpc", StringComparison.OrdinalIgnoreCase))
        {
            exporter.Protocol = OtlpExportProtocol.Grpc;
        }
    }

    private static bool IsHealthEndpoint(PathString path) =>
        HealthEndpointPaths.Any(healthPath => path.Equals(healthPath, StringComparison.OrdinalIgnoreCase));
}
