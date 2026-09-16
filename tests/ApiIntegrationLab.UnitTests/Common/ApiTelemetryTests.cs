using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using ApiIntegrationLab.Api.Common.Telemetry;
using ApiIntegrationLab.UnitTests.TestDoubles;

namespace ApiIntegrationLab.UnitTests.Common;

public sealed class ApiTelemetryTests
{
    [Fact]
    public void Request_measurements_emit_only_bounded_dimensions()
    {
        var measurements = new ConcurrentBag<(string Name, string[] TagKeys)>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, currentListener) =>
        {
            if (instrument.Meter.Name == ApiTelemetry.MeterName)
                currentListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, tags.ToArray().Select(tag => tag.Key).ToArray())));
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, tags.ToArray().Select(tag => tag.Key).ToArray())));
        listener.Start();

        var telemetry = new ApiTelemetry();
        using (var measurement = telemetry.MeasureRequest("github", "profile"))
            measurement.Succeed();

        Assert.NotEmpty(measurements);
        Assert.All(measurements, measurement => Assert.All(
            measurement.TagKeys,
            key => Assert.Contains(key, new[] { "provider", "operation", "outcome", "error.type" })));
    }

    [Fact]
    public void Request_measurements_reject_unbounded_provider_values()
    {
        var telemetry = new ApiTelemetry();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            telemetry.MeasureRequest("provider-from-user-input", "profile"));
    }

    [Fact]
    public void Request_measurements_reject_undefined_provider_operation_pairs()
    {
        var telemetry = new ApiTelemetry();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            telemetry.MeasureRequest("public", "users"));
    }

    [Fact]
    public void Cancel_records_cancelled_without_an_error_type()
    {
        using var activities = TelemetryTestListener.ListenTo(ApiTelemetry.ActivitySourceName);
        var telemetry = new ApiTelemetry();

        using (var measurement = telemetry.MeasureRequest("github", "profile"))
            measurement.Cancel();

        Assert.Contains("outcome=cancelled", activities.SerializedActivities, StringComparison.Ordinal);
        Assert.DoesNotContain("error.type", activities.SerializedActivities, StringComparison.Ordinal);
    }
}
