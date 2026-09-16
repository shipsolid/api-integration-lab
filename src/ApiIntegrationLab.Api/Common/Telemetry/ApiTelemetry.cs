using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ApiIntegrationLab.Api.Common.Telemetry;

public sealed class ApiTelemetry
{
    public const string ActivitySourceName = "ApiIntegrationLab";
    public const string MeterName = "ApiIntegrationLab";

    private static readonly HashSet<(string Provider, string Operation)> ProviderOperations =
        new()
        {
            ("public", "posts"),
            ("basic", "profile"),
            ("github", "profile"),
            ("github", "repositories"),
            ("github", "rate_limit"),
            ("microsoft", "profile"),
            ("microsoft", "users")
        };
    private static readonly HashSet<string> TokenFlows =
        new(StringComparer.Ordinal) { "delegated", "application" };
    private static readonly HashSet<string> Outcomes =
        new(StringComparer.Ordinal) { "success", "error", "cancelled" };
    private static readonly HashSet<string> ErrorTypes =
        new(StringComparer.Ordinal)
        {
            "authentication", "authorization", "configuration", "throttled", "timeout", "upstream"
        };

    private readonly Counter<long> _requests;
    private readonly Histogram<double> _requestDuration;
    private readonly Counter<long> _errors;
    private readonly Counter<long> _tokenRequests;

    public ApiTelemetry()
    {
        _requests = TelemetryInstruments.Meter.CreateCounter<long>("api.client.requests");
        _requestDuration = TelemetryInstruments.Meter.CreateHistogram<double>(
            "api.client.request.duration",
            unit: "s");
        _errors = TelemetryInstruments.Meter.CreateCounter<long>("api.client.errors");
        _tokenRequests = TelemetryInstruments.Meter.CreateCounter<long>("api.auth.token.requests");
    }

    public IIntegrationRequestMeasurement MeasureRequest(string provider, string operation)
    {
        // Validate the pair rather than two independent allowlists. That makes the documented
        // seven-pair cardinality ceiling executable and prevents accidental cross-product growth.
        if (!ProviderOperations.Contains((provider, operation)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(operation),
                $"{provider}/{operation}",
                "Provider and operation are outside the bounded telemetry schema.");
        }
        return new IntegrationRequestMeasurement(this, provider, operation);
    }

    public void RecordTokenRequest(string flow, string outcome)
    {
        RequireBounded(nameof(flow), flow, TokenFlows);
        RequireBounded(nameof(outcome), outcome, Outcomes);
        _tokenRequests.Add(1, new TagList { { "flow", flow }, { "outcome", outcome } });
    }

    private static void RequireBounded(string parameter, string value, HashSet<string> allowed)
    {
        if (!allowed.Contains(value))
            throw new ArgumentOutOfRangeException(parameter, value, "Value is outside the bounded telemetry schema.");
    }

    private void Record(
        string provider,
        string operation,
        string outcome,
        string? errorType,
        TimeSpan duration)
    {
        RequireBounded(nameof(outcome), outcome, Outcomes);
        var tags = new TagList
        {
            { "provider", provider },
            { "operation", operation },
            { "outcome", outcome }
        };
        _requests.Add(1, tags);
        _requestDuration.Record(duration.TotalSeconds, tags);

        if (errorType is null)
            return;

        RequireBounded(nameof(errorType), errorType, ErrorTypes);
        tags.Add("error.type", errorType);
        _errors.Add(1, tags);
    }

    private sealed class IntegrationRequestMeasurement : IIntegrationRequestMeasurement
    {
        private readonly ApiTelemetry _owner;
        private readonly string _provider;
        private readonly string _operation;
        private readonly Activity? _activity;
        private readonly long _started = Stopwatch.GetTimestamp();
        private bool _completed;

        public IntegrationRequestMeasurement(ApiTelemetry owner, string provider, string operation)
        {
            _owner = owner;
            _provider = provider;
            _operation = operation;
            _activity = Activities.StartActivity("integration.request", ActivityKind.Client);
            _activity?.SetTag("provider", provider);
            _activity?.SetTag("operation", operation);
        }

        public void Succeed()
        {
            Complete("success", null);
        }

        public void Fail(string boundedErrorType)
        {
            Complete("error", boundedErrorType);
        }

        public void Cancel()
        {
            Complete("cancelled", null);
        }

        public void Dispose()
        {
            if (!_completed)
                Complete("error", "upstream");
        }

        private void Complete(string outcome, string? errorType)
        {
            if (_completed)
                return;

            _completed = true;
            _activity?.SetTag("outcome", outcome);
            if (errorType is not null)
                _activity?.SetTag("error.type", errorType);
            _activity?.Dispose();
            _owner.Record(
                _provider,
                _operation,
                outcome,
                errorType,
                Stopwatch.GetElapsedTime(_started));
        }
    }

    private static class TelemetryInstruments
    {
        internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);
        internal static readonly Meter Meter = new(MeterName);
    }

    public static ActivitySource Activities => TelemetryInstruments.ActivitySource;
}

public interface IIntegrationRequestMeasurement : IDisposable
{
    void Succeed();
    void Fail(string boundedErrorType);
    void Cancel();
}
