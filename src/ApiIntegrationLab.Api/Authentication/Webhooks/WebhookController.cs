using System.Text.Json;
using ApiIntegrationLab.Api.Common.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ApiIntegrationLab.Api.Authentication.Webhooks;

[ApiController]
[AllowAnonymous]
[Route("api/webhooks")]
public sealed class WebhookController : ControllerBase
{
    private readonly IWebhookSignatureVerifier _verifier;
    private readonly ISystemClock _clock;
    private readonly WebhookOptions _options;

    public WebhookController(
        IWebhookSignatureVerifier verifier,
        ISystemClock clock,
        IOptions<WebhookOptions> options)
    {
        _verifier = verifier;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>Authenticates an inbound event with a replay-resistant HMAC-SHA256 signature.</summary>
    /// <remarks>
    /// Send the Unix timestamp in X-Webhook-Timestamp and lowercase
    /// sha256=&lt;hex&gt; in X-Webhook-Signature-256. Sign the exact bytes
    /// &lt;timestamp&gt;.&lt;request-body&gt;; timestamps may differ by at most five minutes.
    /// </remarks>
    [HttpPost("events")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        if (Request.ContentLength > _options.MaxBodyBytes)
            return PayloadTooLarge();

        var body = await ReadBoundedBodyAsync(Request.Body, _options.MaxBodyBytes, cancellationToken);
        if (body is null)
            return PayloadTooLarge();

        var timestamp = Request.Headers.TryGetValue("X-Webhook-Timestamp", out var timestampHeader)
            ? timestampHeader.ToString()
            : null;
        var signature = Request.Headers.TryGetValue("X-Webhook-Signature-256", out var signatureHeader)
            ? signatureHeader.ToString()
            : null;

        // Verify the untouched wire bytes before deserialization. Re-serializing JSON can change
        // whitespace/property order and would validate bytes the sender never signed.
        var verification = _verifier.Verify(body, timestamp, signature, _clock.UtcNow);
        if (!verification.IsValid)
        {
            var status = verification.Failure == "webhook_not_configured"
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status401Unauthorized;
            return Problem(
                statusCode: status,
                title: "Webhook authentication failed",
                detail: verification.Failure);
        }

        WebhookEvent? webhookEvent;
        try
        {
            webhookEvent = JsonSerializer.Deserialize<WebhookEvent>(
                body,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid webhook payload",
                detail: "The signed body must be valid JSON.");
        }

        if (string.IsNullOrWhiteSpace(webhookEvent?.EventType))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid webhook payload",
                detail: "eventType is required.");
        }

        // Echo only routing metadata, never the full event body or signature material.
        return Accepted(new { webhookEvent.EventType, receivedAt = _clock.UtcNow });
    }

    private IActionResult PayloadTooLarge() =>
        Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            title: "Webhook payload too large",
            detail: $"The request body limit is {_options.MaxBodyBytes} bytes.");

    private static async Task<byte[]?> ReadBoundedBodyAsync(
        Stream stream,
        int maxBytes,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[maxBytes + 1];
        var bytesRead = 0;
        while (bytesRead < buffer.Length)
        {
            var count = await stream.ReadAsync(
                buffer.AsMemory(bytesRead, buffer.Length - bytesRead),
                cancellationToken);
            if (count == 0)
                break;
            bytesRead += count;
        }

        return bytesRead > maxBytes ? null : buffer[..bytesRead];
    }
}
