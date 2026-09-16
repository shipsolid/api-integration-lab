using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace ApiIntegrationLab.IntegrationTests;

public sealed class TelemetryCorrelationTests
{
    [Fact]
    public async Task Problem_details_uses_the_incoming_trace_id_and_telemetry_is_registered()
    {
        const string traceId = "1234567890abcdef1234567890abcdef";
        using var factory = new ApplicationFactory();
        Assert.NotNull(factory.Services.GetService<TracerProvider>());
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/basic/profile");
        request.Headers.TryAddWithoutValidation(
            "traceparent",
            $"00-{traceId}-1234567890abcdef-01");

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(traceId, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Structured_logs_exclude_webhook_body_signature_and_secret()
    {
        const string secret = "never-log-webhook-secret";
        const string bodyMarker = "never-log-webhook-body";
        var logs = new CapturingLoggerProvider();
        using var factory = new ApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Webhook:Secret"] = secret
                }));
            builder.ConfigureLogging(logging => logging.AddProvider(logs));
        });
        using var client = factory.CreateClient();
        var body = Encoding.UTF8.GetBytes($"{{\"eventType\":\"{bodyMarker}\"}}");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(secret, timestamp, body);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/events")
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        request.Headers.Add("X-Webhook-Signature-256", signature);

        using var response = await client.SendAsync(request);
        var serializedLogs = logs.SerializedLogs;

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.DoesNotContain(secret, serializedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(bodyMarker, serializedLogs, StringComparison.Ordinal);
        Assert.DoesNotContain(signature, serializedLogs, StringComparison.Ordinal);
    }

    private static string Sign(string secret, string timestamp, byte[] body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload, prefix.Length);
        return $"sha256={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload)).ToLowerInvariant()}";
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentBag<string> _logs = [];

        public string SerializedLogs => string.Join("\n", _logs);

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_logs);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly ConcurrentBag<string> _logs;

        public CapturingLogger(ConcurrentBag<string> logs)
        {
            _logs = logs;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _logs.Add(formatter(state, exception));
    }
}
