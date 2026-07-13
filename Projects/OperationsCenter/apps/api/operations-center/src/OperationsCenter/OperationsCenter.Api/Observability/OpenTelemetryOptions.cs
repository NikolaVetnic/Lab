namespace OperationsCenter.Api.Observability;

/// <summary>
/// Strongly typed configuration for OpenTelemetry export. Bound from the
/// <c>OpenTelemetry</c> configuration section and overridable through environment
/// variables (for example <c>OpenTelemetry__Enabled</c>, <c>OpenTelemetry__OtlpEndpoint</c>).
/// </summary>
public sealed class OpenTelemetryOptions
{
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// When <see langword="false"/> no OpenTelemetry providers are registered and the
    /// application uses its default logging only. Telemetry is opt-in so the API never
    /// depends on a reachable collector.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>Logical service name reported as the <c>service.name</c> resource attribute.</summary>
    public string ServiceName { get; init; } = "operations-center-api";

    /// <summary>Service version reported as the <c>service.version</c> resource attribute.</summary>
    public string ServiceVersion { get; init; } = "1.0.0";

    /// <summary>OTLP endpoint the exporter sends telemetry to.</summary>
    public string OtlpEndpoint { get; init; } = "http://localhost:4317";

    /// <summary>
    /// Optional OTLP protocol override. Accepts <c>grpc</c> (default) or <c>http/protobuf</c>.
    /// </summary>
    public string? OtlpProtocol { get; init; }
}
