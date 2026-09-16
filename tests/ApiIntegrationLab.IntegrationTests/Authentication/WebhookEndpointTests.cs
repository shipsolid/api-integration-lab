using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiIntegrationLab.IntegrationTests.Authentication;

public sealed class WebhookEndpointTests
{
    private const string Secret = "integration-test-webhook-secret";

    [Fact]
    public async Task Events_accepts_a_valid_signature_and_returns_safe_acknowledgement()
    {
        var body = Encoding.UTF8.GetBytes("{\"eventType\":\"demo.created\",\"sensitive\":\"never-echo\"}");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        using var response = await SendAsync(body, timestamp, Sign(timestamp, body));
        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Contains("\"eventType\":\"demo.created\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain("never-echo", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Events_rejects_an_invalid_signature_before_parsing_json()
    {
        var body = "not-json"u8.ToArray();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var response = await SendAsync(timestamp: timestamp, body: body, signature: "sha256=" + new string('0', 64));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Events_rejects_a_signed_event_without_event_type()
    {
        var body = "{\"value\":1}"u8.ToArray();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var response = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Events_rejects_a_body_larger_than_64_kib()
    {
        var body = new byte[(64 * 1024) + 1];
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        using var response = await SendAsync(body, timestamp, Sign(timestamp, body));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Events_rejects_an_identical_valid_delivery_replay()
    {
        var body = "{\"eventType\":\"demo.created\",\"value\":1}"u8.ToArray();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Sign(timestamp, body);
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var first = await SendAsync(client, body, timestamp, signature);
        using var replay = await SendAsync(client, body, timestamp, signature);
        var replayBody = await replay.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Contains("signature_replayed", replayBody, StringComparison.Ordinal);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        byte[] body,
        string timestamp,
        string signature)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        return await SendAsync(client, body, timestamp, signature);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        byte[] body,
        string timestamp,
        string signature)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/events")
        {
            Content = new ByteArrayContent(body)
        };
        request.Headers.Add("X-Webhook-Timestamp", timestamp);
        request.Headers.Add("X-Webhook-Signature-256", signature);
        return await client.SendAsync(request);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new ApplicationFactory().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Webhook:Secret"] = Secret
                })));

    private static string Sign(string timestamp, byte[] body)
    {
        var prefix = Encoding.UTF8.GetBytes($"{timestamp}.");
        var payload = new byte[prefix.Length + body.Length];
        prefix.CopyTo(payload, 0);
        body.CopyTo(payload, prefix.Length);
        var digest = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), payload);
        return $"sha256={Convert.ToHexString(digest).ToLowerInvariant()}";
    }
}
